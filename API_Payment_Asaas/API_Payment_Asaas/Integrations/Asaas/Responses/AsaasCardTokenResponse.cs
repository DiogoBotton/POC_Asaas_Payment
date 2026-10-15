using System.Text.Json.Serialization;

namespace API_Payment_Asaas.Integrations.Asaas.Responses;

internal sealed record AsaasCardTokenResponse(
    [property: JsonPropertyName("creditCardToken")] string? CreditCardToken,
    [property: JsonPropertyName("creditCardBrand")] string? CreditCardBrand,
    [property: JsonPropertyName("creditCardNumber")] string? CreditCardNumber);