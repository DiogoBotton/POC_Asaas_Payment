using System.Text.Json.Serialization;

namespace API_Payment_Asaas.Integrations.Asaas.Responses;

internal sealed record AsaasPixResponse(
    [property: JsonPropertyName("payload")] string? Payload,
    [property: JsonPropertyName("encodedImage")] string? EncodedImage);