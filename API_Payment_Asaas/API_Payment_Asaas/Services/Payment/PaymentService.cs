using API_Payment_Asaas.Data;
using API_Payment_Asaas.DTOs.Customers;
using API_Payment_Asaas.DTOs.Payments;
using API_Payment_Asaas.Integrations.Asaas.Contracts;
using API_Payment_Asaas.Integrations.Asaas.Exceptions;
using API_Payment_Asaas.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace API_Payment_Asaas.Services;

public sealed class PaymentService(
    PaymentDbContext dbContext,
    IAsaasClient asaasClient,
    ICustomerService customerService,
    ICardTokenProtector cardTokenProtector,
    IHttpContextAccessor httpContextAccessor,
    IOptions<AsaasOptions> asaasOptions,
    ILogger<PaymentService> logger,
    IMemoryCache memoryCache) : IPaymentService
{
    public async Task<PaymentResponse> CreateAsync(CreatePaymentRequest request, CancellationToken cancellationToken)
    {
        if (request.DueDate < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ArgumentException("O vencimento não pode estar no passado.");
        }

        var paymentMethod = request.PaymentMethod ?? throw new ArgumentException("Escolha um meio de pagamento.");
        if (paymentMethod is not (PaymentMethod.CreditCard or PaymentMethod.Pix or PaymentMethod.Boleto))
        {
            throw new ArgumentException("Escolha cartão de crédito, PIX ou boleto.");
        }

        if (request.SaveCardForFutureUse && paymentMethod != PaymentMethod.CreditCard)
        {
            throw new ArgumentException("Somente cartões podem ser salvos para uso futuro.");
        }

        var validatedCard = paymentMethod == PaymentMethod.CreditCard
            ? CreditCardValidation.Validate(request.CreditCard, request.CreditCardHolderInfo)
            : null;
        var remoteIp = paymentMethod == PaymentMethod.CreditCard ? GetRemoteIp() : null;

        var customer = await customerService.FindOrCreateAndSyncAsync(new CustomerProfile(
            request.Name,
            request.Email,
            request.CpfCnpj,
            request.Phone,
            request.PostalCode,
            request.Address,
            request.AddressNumber,
            request.AddressComplement,
            request.Province), cancellationToken);

        var payment = new Payment
        {
            CustomerId = customer.Id,
            Value = request.Value,
            PaymentMethod = paymentMethod,
            Status = PaymentStatus.CREATING,
            DueDate = request.DueDate
        };
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync(cancellationToken);

        AsaasPaymentResult asaasPayment;
        AsaasTokenizedCardResult? tokenizedCard = null;
        try
        {
            if (request.SaveCardForFutureUse && validatedCard is not null)
            {
                tokenizedCard = await asaasClient.TokenizeCardAsync(
                    customer.AsaasCustomerId,
                    validatedCard.Card,
                    validatedCard.Holder,
                    remoteIp!,
                    cancellationToken);
            }

            asaasPayment = await asaasClient.CreateDirectPaymentAsync(
                payment.Id,
                customer.AsaasCustomerId,
                payment.Value,
                payment.DueDate,
                request.Description,
                paymentMethod,
                validatedCard?.Card,
                validatedCard?.Holder,
                tokenizedCard?.Token,
                remoteIp,
                request.DeviceInfo,
                asaasOptions.Value.EnableThreeDSecure,
                new Uri(new Uri(asaasOptions.Value.FrontendBaseUrl.TrimEnd('/') + "/"), $"payments/{payment.Id}").ToString(),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            payment.Status = PaymentStatus.CREATION_FAILED;
            payment.StatusUnknown = null;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }

        payment.AsaasPaymentId = asaasPayment.Id;
        payment.Status = asaasPayment.Status;
        payment.StatusUnknown = asaasPayment.StatusUnknown;
        payment.PaymentMethod = ParsePaymentMethod(asaasPayment.BillingType);
        payment.BankSlipUrl = asaasPayment.BankSlipUrl;
        if (request.SaveCardForFutureUse && tokenizedCard is not null)
        {
            var protectedCard = cardTokenProtector.Protect(customer.Id, tokenizedCard);
            var alreadyStored = await dbContext.AsaasStoredCards.AnyAsync(
                storedCard => storedCard.TokenFingerprint == protectedCard.TokenFingerprint,
                cancellationToken);
            if (!alreadyStored)
            {
                dbContext.AsaasStoredCards.Add(protectedCard);
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        if (payment.PaymentMethod == PaymentMethod.Pix)
        {
            await FetchPixInstructionsAsync(payment, cancellationToken);
        }
        else if (payment.PaymentMethod == PaymentMethod.Boleto)
        {
            try
            {
                payment.BankSlipIdentificationField = await asaasClient.GetBoletoIdentificationFieldAsync(
                    asaasPayment.Id,
                    cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Payment {PaymentId} was created but its boleto line could not be retrieved.", payment.Id);
            }
        }

        return ToResponse(payment, asaasPayment.ThreeDSecureChallengeUrl);
    }

    public async Task<IReadOnlyList<PaymentResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var payments = await dbContext.Payments
            .AsNoTracking()
            .OrderByDescending(payment => payment.CreatedAt)
            .Select(payment => new PaymentResponse(
                payment.Id,
                payment.Value,
                payment.PaymentMethod,
                payment.Status,
                payment.DueDate,
                payment.BankSlipUrl,
                payment.BankSlipIdentificationField,
                payment.PixCopyPaste,
                payment.PixQrCodeUrl,
                null,
                payment.Invoice == null ? null : payment.Invoice.Status,
                payment.Invoice == null ? null : payment.Invoice.PdfUrl,
                payment.Invoice == null ? null : payment.Invoice.XmlUrl,
                payment.CreatedAt,
                payment.StatusUnknown,
                payment.Invoice == null ? null : payment.Invoice.StatusUnknown))
            .ToListAsync(cancellationToken);

        return payments;
    }

    public async Task<PaymentResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments
            .Include(candidate => candidate.Invoice)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (payment is null)
        {
            return null;
        }

        var retryKey = $"pix-instructions-retry:{payment.Id}";
        if (payment.PaymentMethod == PaymentMethod.Pix &&
            string.IsNullOrWhiteSpace(payment.PixCopyPaste) &&
            string.IsNullOrWhiteSpace(payment.PixQrCodeUrl) &&
            !string.IsNullOrWhiteSpace(payment.AsaasPaymentId) &&
            !memoryCache.TryGetValue(retryKey, out _))
        {
            memoryCache.Set(retryKey, true, TimeSpan.FromMinutes(1));
            await FetchPixInstructionsAsync(payment, cancellationToken);
        }

        return ToResponse(payment);
    }

    private async Task FetchPixInstructionsAsync(Payment payment, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payment.AsaasPaymentId))
        {
            return;
        }

        try
        {
            var pix = await asaasClient.GetPixQrCodeAsync(payment.AsaasPaymentId, cancellationToken);
            payment.PixCopyPaste = pix.Payload;
            payment.PixQrCodeUrl = pix.EncodedImage is null ? null : $"data:image/png;base64,{pix.EncodedImage}";
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Payment {PaymentId} was created but Pix instructions could not be retrieved.", payment.Id);
        }
    }

    private static PaymentMethod ParsePaymentMethod(string billingType) => billingType switch
    {
        "PIX" => PaymentMethod.Pix,
        "BOLETO" => PaymentMethod.Boleto,
        "CREDIT_CARD" => PaymentMethod.CreditCard,
        _ => PaymentMethod.Undefined
    };

    private PaymentResponse ToResponse(Payment payment, string? threeDSecureChallengeUrl = null) => new(
        payment.Id,
        payment.Value,
        payment.PaymentMethod,
        payment.Status,
        payment.DueDate,
        payment.BankSlipUrl,
        payment.BankSlipIdentificationField,
        payment.PixCopyPaste,
        payment.PixQrCodeUrl,
        threeDSecureChallengeUrl,
        payment.Invoice?.Status,
        payment.Invoice?.PdfUrl,
        payment.Invoice?.XmlUrl,
        payment.CreatedAt,
        payment.StatusUnknown,
        payment.Invoice?.StatusUnknown);

    private string GetRemoteIp()
    {
        var address = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress
            ?? throw new InvalidOperationException("Não foi possível identificar o IP do pagador.");
        return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }

}