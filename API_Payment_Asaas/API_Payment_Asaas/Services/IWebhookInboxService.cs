using API_Payment_Asaas.Data;
using API_Payment_Asaas.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace API_Payment_Asaas.Services;

public interface IWebhookInboxService
{
    Task<bool> EnqueueAsync(string eventId, string eventType, string payload, CancellationToken cancellationToken);
}

public sealed class WebhookInboxService(PaymentDbContext dbContext) : IWebhookInboxService
{
    public async Task<bool> EnqueueAsync(
        string eventId,
        string eventType,
        string payload,
        CancellationToken cancellationToken)
    {
        if (await dbContext.WebhookEvents.AnyAsync(item => item.AsaasEventId == eventId, cancellationToken))
        {
            return false;
        }

        dbContext.WebhookEvents.Add(new WebhookEvent
        {
            AsaasEventId = eventId,
            EventType = eventType,
            Payload = payload
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return false;
        }
    }
}