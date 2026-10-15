namespace API_Payment_Asaas.Models;

public sealed class AsaasStoredCard
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public required string ProtectedToken { get; set; }
    public required string TokenFingerprint { get; set; }
    public required string Brand { get; set; }
    public required string LastFourDigits { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}