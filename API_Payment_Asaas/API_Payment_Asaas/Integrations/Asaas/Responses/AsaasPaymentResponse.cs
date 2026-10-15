using System.Text.Json.Serialization;

namespace API_Payment_Asaas.Integrations.Asaas.Responses;

internal sealed record AsaasPaymentResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("billingType")] string BillingType,
    [property: JsonPropertyName("dueDate")] string DueDate,
    [property: JsonPropertyName("bankSlipUrl")] string? BankSlipUrl,
    [property: JsonPropertyName("threeDSecureChallengeUrl")] string? ThreeDSecureChallengeUrl);