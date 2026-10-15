namespace API_Payment_Asaas.Models;

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