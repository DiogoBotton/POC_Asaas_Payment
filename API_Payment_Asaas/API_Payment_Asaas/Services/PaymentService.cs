using API_Payment_Asaas.Data;
using API_Payment_Asaas.Models;
using Microsoft.EntityFrameworkCore;

namespace API_Payment_Asaas.Services;

public sealed class PaymentService(
    PaymentDbContext dbContext,
    IAsaasClient asaasClient,
    ILogger<PaymentService> logger) : IPaymentService
{
    public async Task<PaymentResponse> CreateAsync(CreatePaymentRequest request, CancellationToken cancellationToken)
    {
        if (request.DueDate < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ArgumentException("Due date cannot be in the past.");
        }

        if (request.CheckoutMode == CheckoutMode.Direct && request.PaymentMethod == PaymentMethod.Undefined)
        {
            throw new ArgumentException("Choose a payment method for direct checkout.");
        }

        if (request.CheckoutMode == CheckoutMode.Direct && request.PaymentMethod == PaymentMethod.CreditCard)
        {
            throw new ArgumentException("Use hosted checkout for credit-card payments until direct card tokenization is configured.");
        }

        var billingType = request.CheckoutMode == CheckoutMode.Hosted
            ? PaymentMethod.Undefined
            : request.PaymentMethod;

        var customer = await dbContext.Customers
            .FirstOrDefaultAsync(candidate => candidate.Email == request.Email, cancellationToken);

        if (customer is null)
        {
            var asaasCustomer = await asaasClient.CreateCustomerAsync(
                request.Name,
                request.Email,
                request.CpfCnpj,
                request.Phone,
                cancellationToken);

            customer = new Customer
            {
                AsaasCustomerId = asaasCustomer.Id,
                Name = request.Name,
                Email = request.Email,
                CpfCnpj = request.CpfCnpj,
                Phone = request.Phone
            };
            dbContext.Customers.Add(customer);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var payment = new Payment
        {
            CustomerId = customer.Id,
            Value = request.Value,
            PaymentMethod = billingType,
            Status = "CREATING",
            DueDate = request.DueDate
        };
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync(cancellationToken);

        AsaasPaymentResult asaasPayment;
        try
        {
            asaasPayment = await asaasClient.CreatePaymentAsync(
                payment.Id,
                customer.AsaasCustomerId,
                payment.Value,
                payment.DueDate,
                billingType,
                request.Description,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            payment.Status = "CREATION_FAILED";
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }

        payment.AsaasPaymentId = asaasPayment.Id;
        payment.Status = asaasPayment.Status;
        payment.PaymentMethod = ParsePaymentMethod(asaasPayment.BillingType);
        payment.InvoiceUrl = asaasPayment.InvoiceUrl;
        payment.BankSlipUrl = asaasPayment.BankSlipUrl;
        await dbContext.SaveChangesAsync(cancellationToken);

        if (payment.PaymentMethod == PaymentMethod.Pix)
        {
            try
            {
                var pix = await asaasClient.GetPixQrCodeAsync(asaasPayment.Id, cancellationToken);
                payment.PixCopyPaste = pix.Payload;
                payment.PixQrCodeUrl = pix.EncodedImage is null ? null : $"data:image/png;base64,{pix.EncodedImage}";
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Payment {PaymentId} was created but Pix instructions could not be retrieved.", payment.Id);
            }
        }

        return ToResponse(payment);
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
                payment.InvoiceUrl,
                payment.BankSlipUrl,
                payment.PixCopyPaste,
                payment.PixQrCodeUrl,
                payment.Invoice == null ? null : payment.Invoice.Status,
                payment.Invoice == null ? null : payment.Invoice.PdfUrl,
                payment.Invoice == null ? null : payment.Invoice.XmlUrl,
                payment.CreatedAt))
            .ToListAsync(cancellationToken);

        return payments;
    }

    public async Task<PaymentResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments.AsNoTracking()
            .Include(candidate => candidate.Invoice)
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return payment is null ? null : ToResponse(payment);
    }

    private static PaymentMethod ParsePaymentMethod(string billingType) => billingType switch
    {
        "PIX" => PaymentMethod.Pix,
        "BOLETO" => PaymentMethod.Boleto,
        "CREDIT_CARD" => PaymentMethod.CreditCard,
        _ => PaymentMethod.Undefined
    };

    private static PaymentResponse ToResponse(Payment payment) => new(
        payment.Id,
        payment.Value,
        payment.PaymentMethod,
        payment.Status,
        payment.DueDate,
        payment.InvoiceUrl,
        payment.BankSlipUrl,
        payment.PixCopyPaste,
        payment.PixQrCodeUrl,
        payment.Invoice?.Status,
        payment.Invoice?.PdfUrl,
        payment.Invoice?.XmlUrl,
        payment.CreatedAt);
}