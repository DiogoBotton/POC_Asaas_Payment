using System.Text.Json.Serialization;

namespace API_Payment_Asaas.Integrations.Asaas.Responses;

internal sealed record AsaasSubscriptionResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("nextDueDate")] string NextDueDate);