namespace API_Payment_Asaas.Services;

public interface IWebhookProcessor
{
    Task ProcessPendingAsync(CancellationToken cancellationToken);
}