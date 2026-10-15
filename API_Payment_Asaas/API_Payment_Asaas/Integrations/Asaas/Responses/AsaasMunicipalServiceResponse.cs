using System.Text.Json.Serialization;

namespace API_Payment_Asaas.Integrations.Asaas.Responses;

internal sealed record AsaasMunicipalServiceResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("issTax")] decimal? IssTax);