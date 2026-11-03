namespace API_Payment_Asaas.Models;

public sealed class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? AsaasInvoiceId { get; set; }
    public Guid PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;
    public InvoiceWorkflowStatus? WorkflowStatus { get; set; }
    public string? AsaasStatus { get; set; }
    public string? PdfUrl { get; set; }
    public string? XmlUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public string? FiscalMunicipalServiceId { get; set; }
    public string? FiscalMunicipalServiceName { get; set; }
    public string? FiscalServiceDescription { get; set; }
    public string? FiscalObservations { get; set; }
    public bool? FiscalRetainIss { get; set; }
    public decimal? FiscalIss { get; set; }
    public decimal? FiscalPis { get; set; }
    public decimal? FiscalCofins { get; set; }
    public decimal? FiscalCsll { get; set; }
    public decimal? FiscalInss { get; set; }
    public decimal? FiscalIr { get; set; }
    public int ScheduleAttempts { get; set; }
    public DateTimeOffset? AuthorizedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}