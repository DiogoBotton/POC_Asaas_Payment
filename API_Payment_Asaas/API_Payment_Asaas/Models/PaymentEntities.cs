namespace API_Payment_Asaas.Models;

public enum PaymentMethod
{
    Undefined,
    Pix,
    Boleto,
    CreditCard
}

public enum BillingCycle
{
    Monthly
}

public sealed class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string AsaasCustomerId { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public string? CpfCnpj { get; set; }
    public string? Phone { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<Subscription> Subscriptions { get; set; } = [];
}

public sealed class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? AsaasSubscriptionId { get; set; }
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public decimal Value { get; set; }
    public BillingCycle Cycle { get; set; } = BillingCycle.Monthly;
    public PaymentMethod PaymentMethod { get; set; }
    public required string Status { get; set; }
    public DateOnly NextDueDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<Payment> Payments { get; set; } = [];
}

public sealed class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? AsaasPaymentId { get; set; }
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public Guid? SubscriptionId { get; set; }
    public Subscription? Subscription { get; set; }
    public decimal Value { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public required string Status { get; set; }
    public DateOnly DueDate { get; set; }
    public string? InvoiceUrl { get; set; }
    public string? BankSlipUrl { get; set; }
    public string? PixCopyPaste { get; set; }
    public string? PixQrCodeUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Invoice? Invoice { get; set; }
}

public sealed class Invoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? AsaasInvoiceId { get; set; }
    public Guid PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;
    public required string Status { get; set; }
    public string? PdfUrl { get; set; }
    public string? XmlUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public int ScheduleAttempts { get; set; }
    public DateTimeOffset? AuthorizedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class WebhookEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string AsaasEventId { get; set; }
    public required string EventType { get; set; }
    public required string Payload { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}