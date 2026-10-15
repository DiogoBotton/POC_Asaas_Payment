using API_Payment_Asaas.Models;

namespace API_Payment_Asaas.Integrations.Asaas.Contracts;

public sealed record AsaasInvoiceResult(string Id, InvoiceStatus Status, string? StatusUnknown, string? PdfUrl, string? XmlUrl);