namespace API_Payment_Asaas.Integrations.Asaas.Contracts;

public sealed record AsaasInvoiceResult(string Id, string? Status, string? PdfUrl, string? XmlUrl);