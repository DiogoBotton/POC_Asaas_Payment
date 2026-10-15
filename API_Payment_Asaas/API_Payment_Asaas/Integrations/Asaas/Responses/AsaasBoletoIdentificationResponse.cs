using System.Text.Json.Serialization;

namespace API_Payment_Asaas.Integrations.Asaas.Responses;

internal sealed record AsaasBoletoIdentificationResponse(
    [property: JsonPropertyName("identificationField")] string? IdentificationField);