using System.Text.Json.Serialization;

namespace API_Payment_Asaas.Integrations.Asaas.Responses;

internal sealed record AsaasInvoiceResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("pdfUrl")] string? PdfUrl,
    [property: JsonPropertyName("xmlUrl")] string? XmlUrl);