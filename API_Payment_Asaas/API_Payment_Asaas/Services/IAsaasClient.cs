using System.Net;
using API_Payment_Asaas.Models;

namespace API_Payment_Asaas.Services;

public interface IAsaasClient
{
    bool CanScheduleInvoice { get; }
    Task<AsaasCustomerResult> CreateCustomerAsync(string name, string email, string? cpfCnpj, string? phone, CancellationToken cancellationToken);
    Task<AsaasPaymentResult> CreatePaymentAsync(Guid localPaymentId, string customerId, decimal value, DateOnly dueDate, PaymentMethod paymentMethod, string description, CancellationToken cancellationToken);
    Task<AsaasPixResult> GetPixQrCodeAsync(string paymentId, CancellationToken cancellationToken);
    Task<AsaasInvoiceResult> ScheduleInvoiceAsync(Guid localInvoiceId, string paymentId, decimal value, CancellationToken cancellationToken);
}

public sealed record AsaasCustomerResult(string Id);

public sealed record AsaasPaymentResult(
    string Id,
    string Status,
    string BillingType,
    DateOnly DueDate,
    string? InvoiceUrl,
    string? BankSlipUrl);

public sealed record AsaasPixResult(string? Payload, string? EncodedImage, DateTimeOffset? ExpirationDate);

public sealed record AsaasInvoiceResult(string Id, string Status, string? PdfUrl, string? XmlUrl);

public sealed class AsaasApiException(HttpStatusCode statusCode) : Exception("The Asaas API request failed.")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}