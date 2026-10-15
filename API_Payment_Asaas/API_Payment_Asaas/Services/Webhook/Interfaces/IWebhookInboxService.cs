namespace API_Payment_Asaas.Services;

public interface IWebhookInboxService
{
    Task<bool> EnqueueAsync(string eventId, string eventType, string payload, CancellationToken cancellationToken);
}