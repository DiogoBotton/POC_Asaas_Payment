using API_Payment_Asaas.Models;

namespace API_Payment_Asaas.Integrations.Asaas.Contracts;

public sealed record AsaasPaymentResult(
    string Id,
    PaymentStatus Status,
    string? StatusUnknown,
    string BillingType,
    DateOnly DueDate,
    string? BankSlipUrl,
    string? ThreeDSecureChallengeUrl);