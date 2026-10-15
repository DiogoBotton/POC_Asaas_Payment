namespace API_Payment_Asaas.Models;

public sealed class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string AsaasCustomerId { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public string? CpfCnpj { get; set; }
    public string? Phone { get; set; }
    public string? PostalCode { get; set; }
    public string? Address { get; set; }
    public string? AddressNumber { get; set; }
    public string? AddressComplement { get; set; }
    public string? Province { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<AsaasStoredCard> StoredCards { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<Subscription> Subscriptions { get; set; } = [];
}