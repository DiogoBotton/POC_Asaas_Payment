namespace API_Payment_Asaas.Models;

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
    public PaymentStatus Status { get; set; }
    public string? StatusUnknown { get; set; }
    public DateOnly DueDate { get; set; }
    public string? BankSlipUrl { get; set; }
    public string? BankSlipIdentificationField { get; set; }
    public string? PixCopyPaste { get; set; }
    public string? PixQrCodeUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Invoice? Invoice { get; set; }
}