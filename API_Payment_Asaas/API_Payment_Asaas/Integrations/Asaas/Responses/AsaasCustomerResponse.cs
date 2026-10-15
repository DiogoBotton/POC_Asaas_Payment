using System.Text.Json.Serialization;

namespace API_Payment_Asaas.Integrations.Asaas.Responses;

internal sealed record AsaasCustomerResponse([property: JsonPropertyName("id")] string Id);