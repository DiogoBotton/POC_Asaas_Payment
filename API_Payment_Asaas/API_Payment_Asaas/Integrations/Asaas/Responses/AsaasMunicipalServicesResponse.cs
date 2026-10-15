using System.Text.Json.Serialization;

namespace API_Payment_Asaas.Integrations.Asaas.Responses;

internal sealed record AsaasMunicipalServicesResponse(
    [property: JsonPropertyName("totalCount")] int TotalCount,
    [property: JsonPropertyName("data")] IReadOnlyList<AsaasMunicipalServiceResponse>? Data);