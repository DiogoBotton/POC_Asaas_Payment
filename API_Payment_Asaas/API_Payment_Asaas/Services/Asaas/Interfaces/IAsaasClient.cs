using API_Payment_Asaas.DTOs.Customers;
using API_Payment_Asaas.DTOs.Payments;
using API_Payment_Asaas.Integrations.Asaas.Contracts;
using API_Payment_Asaas.Models;

namespace API_Payment_Asaas.Services;

public interface IAsaasClient
{
    bool CanScheduleInvoice { get; }
    Task<AsaasCustomerResult> CreateCustomerAsync(CustomerProfile customer, CancellationToken cancellationToken);
    Task UpdateCustomerAsync(string customerId, CustomerProfile customer, CancellationToken cancellationToken);
    Task<AsaasTokenizedCardResult> TokenizeCardAsync(string customerId, CreditCardDetails card, CreditCardHolderDetails holder, string remoteIp, CancellationToken cancellationToken);
    Task<AsaasPaymentResult> CreateDirectPaymentAsync(Guid localPaymentId, string customerId, decimal value, DateOnly dueDate, string description, PaymentMethod paymentMethod, CreditCardDetails? creditCard, CreditCardHolderDetails? creditCardHolderInfo, string? creditCardToken, string? remoteIp, CreditCardDeviceInfo? deviceInfo, bool enableThreeDSecure, string threeDSecureCallbackUrl, CancellationToken cancellationToken);
    Task<AsaasSubscriptionResult> CreateSubscriptionAsync(Guid localSubscriptionId, string customerId, decimal value, BillingCycle cycle, DateOnly nextDueDate, PaymentMethod paymentMethod, string description, CreditCardDetails? creditCard, CreditCardHolderDetails? creditCardHolderInfo, string? creditCardToken, string? remoteIp, CancellationToken cancellationToken);
    Task UpdateSubscriptionValueAsync(string subscriptionId, decimal value, CancellationToken cancellationToken);
    Task InactivateSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken);
    Task DeletePaymentAsync(string paymentId, CancellationToken cancellationToken);
    Task<AsaasPixResult> GetPixQrCodeAsync(string paymentId, CancellationToken cancellationToken);
    Task<string?> GetBoletoIdentificationFieldAsync(string paymentId, CancellationToken cancellationToken);
    Task<AsaasInvoiceResult> ScheduleInvoiceAsync(Guid localInvoiceId, string paymentId, decimal value, string? serviceDescription, CancellationToken cancellationToken);
}