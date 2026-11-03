using System.Text.Json.Serialization;

namespace API_Payment_Asaas.Integrations.Asaas.Responses;

internal sealed record AsaasMunicipalServicesResponse(
    [property: JsonPropertyName("offset")] int Offset,
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("totalCount")] int TotalCount,
    [property: JsonPropertyName("hasMore")] bool HasMore,
    [property: JsonPropertyName("data")] IReadOnlyList<AsaasMunicipalServiceResponse>? Data);