using API_Payment_Asaas.Data;
using API_Payment_Asaas.DTOs.Customers;
using API_Payment_Asaas.DTOs.Payments;
using API_Payment_Asaas.DTOs.Subscriptions;
using API_Payment_Asaas.Integrations.Asaas.Contracts;
using API_Payment_Asaas.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace API_Payment_Asaas.Services;

public sealed class SubscriptionService(
    PaymentDbContext dbContext,
    IAsaasClient asaasClient,
    ICustomerService customerService,
    ICardTokenProtector cardTokenProtector,
    IHttpContextAccessor httpContextAccessor) : ISubscriptionService
{
    private static readonly HashSet<PaymentStatus> DeletablePaymentStatuses = [PaymentStatus.PENDING, PaymentStatus.OVERDUE];

    public async Task<IReadOnlyList<SubscriptionResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var subscriptions = await dbContext.Subscriptions
            .AsNoTracking()
            .Include(subscription => subscription.Customer)
            .Include(subscription => subscription.Payments)
            .OrderByDescending(subscription => subscription.CreatedAt)
            .ToListAsync(cancellationToken);

        return subscriptions.Select(ToResponse).ToList();
    }

    public async Task<SubscriptionResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.Subscriptions
            .AsNoTracking()
            .Include(candidate => candidate.Customer)
            .Include(candidate => candidate.Payments)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return subscription is null ? null : ToResponse(subscription);
    }

    public async Task<SubscriptionResponse> CreateAsync(
        CreateSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var paymentMethod = request.PaymentMethod ?? throw new ArgumentException("Escolha um meio de pagamento.");
        var cycle = request.Cycle ?? throw new ArgumentException("Escolha um ciclo de recorrência.");
        var nextDueDate = request.NextDueDate ?? throw new ArgumentException("Informe o primeiro vencimento.");
        if (nextDueDate < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ArgumentException("O primeiro vencimento não pode estar no passado.");
        }

        if (paymentMethod is not (PaymentMethod.CreditCard or PaymentMethod.Pix or PaymentMethod.Boleto))
        {
            throw new ArgumentException("Escolha cartão de crédito, PIX ou boleto para a assinatura.");
        }

        if (request.SaveCardForFutureUse && paymentMethod != PaymentMethod.CreditCard)
        {
            throw new ArgumentException("Somente cartões podem ser salvos para uso futuro.");
        }

        var validatedCard = paymentMethod == PaymentMethod.CreditCard
            ? CreditCardValidation.Validate(request.CreditCard, request.CreditCardHolderInfo)
            : null;
        if (paymentMethod != PaymentMethod.CreditCard &&
            (request.CreditCard is not null || request.CreditCardHolderInfo is not null))
        {
            throw new ArgumentException("Dados de cartão só podem ser enviados para assinaturas com cartão.");
        }

        var remoteIp = validatedCard is null ? null : GetRemoteIp();

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

        var description = string.IsNullOrWhiteSpace(request.Description)
            ? "Assinatura da plataforma"
            : request.Description.Trim();
        var subscription = new Subscription
        {
            Customer = customer,
            CustomerId = customer.Id,
            Value = request.Value,
            PaymentMethod = paymentMethod,
            Cycle = cycle,
            Description = description,
            Status = SubscriptionStatus.CREATING,
            NextDueDate = nextDueDate
        };
        dbContext.Subscriptions.Add(subscription);
        await dbContext.SaveChangesAsync(cancellationToken);

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

            var asaasSubscription = await asaasClient.CreateSubscriptionAsync(
                subscription.Id,
                customer.AsaasCustomerId,
                subscription.Value,
                subscription.Cycle,
                subscription.NextDueDate,
                subscription.PaymentMethod,
                subscription.Description,
                validatedCard?.Card,
                validatedCard?.Holder,
                tokenizedCard?.Token,
                remoteIp,
                cancellationToken);
            subscription.AsaasSubscriptionId = asaasSubscription.Id;
            subscription.Status = asaasSubscription.Status;
            subscription.StatusUnknown = asaasSubscription.StatusUnknown;
            subscription.NextDueDate = asaasSubscription.NextDueDate;

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
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            subscription.Status = SubscriptionStatus.CREATION_FAILED;
            subscription.StatusUnknown = null;
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(subscription);
    }

    public async Task<SubscriptionResponse> UpdateValueAsync(
        Guid id,
        UpdateSubscriptionValueRequest request,
        CancellationToken cancellationToken)
    {
        var subscription = await dbContext.Subscriptions
            .Include(candidate => candidate.Customer)
            .Include(candidate => candidate.Payments)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("A assinatura não foi encontrada.");

        if (subscription.Status != SubscriptionStatus.ACTIVE || string.IsNullOrWhiteSpace(subscription.AsaasSubscriptionId))
        {
            throw new InvalidOperationException("Somente uma assinatura ativa pode ter o valor alterado.");
        }

        await asaasClient.UpdateSubscriptionValueAsync(subscription.AsaasSubscriptionId, request.Value, cancellationToken);
        subscription.Value = request.Value;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(subscription);
    }

    public async Task CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var subscription = await dbContext.Subscriptions
            .Include(candidate => candidate.Payments)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException("A assinatura não foi encontrada.");

        if (subscription.Status != SubscriptionStatus.INACTIVE)
        {
            if (!string.IsNullOrWhiteSpace(subscription.AsaasSubscriptionId))
            {
                await asaasClient.InactivateSubscriptionAsync(subscription.AsaasSubscriptionId, cancellationToken);
            }
            else if (subscription.Status == SubscriptionStatus.CREATING)
            {
                throw new InvalidOperationException("Aguarde a Asaas concluir a criação da assinatura antes de cancelá-la.");
            }

            subscription.Status = SubscriptionStatus.INACTIVE;
            subscription.StatusUnknown = null;
            subscription.CanceledAt = DateTimeOffset.UtcNow;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            subscription.CancellationCutoffDate = subscription.Payments
                .Where(payment => payment.DueDate <= today)
                .OrderByDescending(payment => payment.DueDate)
                .Select(payment => (DateOnly?)payment.DueDate)
                .FirstOrDefault()
                ?? subscription.Payments.OrderBy(payment => payment.DueDate)
                    .Select(payment => (DateOnly?)payment.DueDate)
                    .FirstOrDefault()
                ?? subscription.NextDueDate;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (subscription.CancellationCutoffDate is not { } cutoffDate)
        {
            return;
        }

        var futurePayments = subscription.Payments
            .Where(payment => payment.DueDate > cutoffDate &&
                              DeletablePaymentStatuses.Contains(payment.Status) &&
                              !string.IsNullOrWhiteSpace(payment.AsaasPaymentId))
            .ToList();

        foreach (var payment in futurePayments)
        {
            await asaasClient.DeletePaymentAsync(payment.AsaasPaymentId!, cancellationToken);
            payment.Status = PaymentStatus.DELETED;
            payment.StatusUnknown = null;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static SubscriptionResponse ToResponse(Subscription subscription) => new(
        subscription.Id,
        subscription.Customer.Name,
        subscription.Customer.Email,
        subscription.Value,
        subscription.PaymentMethod,
        subscription.Cycle,
        subscription.Status,
        subscription.NextDueDate,
        subscription.CreatedAt,
        subscription.Payments
            .OrderBy(payment => payment.DueDate)
            .Select(payment => new SubscriptionChargeResponse(
                payment.Id,
                payment.Value,
                payment.PaymentMethod,
                payment.Status,
                payment.DueDate,
                payment.BankSlipIdentificationField,
                payment.BankSlipUrl,
                payment.PixCopyPaste,
                payment.PixQrCodeUrl,
                payment.StatusUnknown))
            .ToList(),
        subscription.StatusUnknown);

    private string GetRemoteIp()
    {
        var address = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress
            ?? throw new InvalidOperationException("Não foi possível identificar o IP do pagador.");
        return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }
}