namespace API_Payment_Asaas.Models;

public sealed class Subscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? AsaasSubscriptionId { get; set; }
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public decimal Value { get; set; }
    public BillingCycle Cycle { get; set; } = BillingCycle.Monthly;
    public PaymentMethod PaymentMethod { get; set; }
    public required string Description { get; set; }
    public SubscriptionStatus Status { get; set; }
    public string? StatusUnknown { get; set; }
    public DateOnly NextDueDate { get; set; }
    public DateOnly? CancellationCutoffDate { get; set; }
    public DateTimeOffset? CanceledAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<Payment> Payments { get; set; } = [];
}