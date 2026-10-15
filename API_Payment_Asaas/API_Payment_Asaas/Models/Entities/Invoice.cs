namespace API_Payment_Asaas.Models;

public sealed class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? AsaasInvoiceId { get; set; }
    public Guid PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;
    public InvoiceStatus Status { get; set; }
    public string? StatusUnknown { get; set; }
    public string? PdfUrl { get; set; }
    public string? XmlUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public int ScheduleAttempts { get; set; }
    public DateTimeOffset? AuthorizedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}