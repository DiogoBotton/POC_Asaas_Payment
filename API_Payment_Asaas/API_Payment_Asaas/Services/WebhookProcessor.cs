using System.Text.Json;
using API_Payment_Asaas.Data;
using API_Payment_Asaas.Models;
using Microsoft.EntityFrameworkCore;

namespace API_Payment_Asaas.Services;

public sealed class WebhookProcessor(
    PaymentDbContext dbContext,
    IAsaasClient asaasClient,
    ILogger<WebhookProcessor> logger) : IWebhookProcessor
{
    private const int MaxAttempts = 10;

    public async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        var pendingEvents = await dbContext.WebhookEvents
            .Where(item => item.ProcessedAt == null && item.Attempts < MaxAttempts)
            .OrderBy(item => item.ReceivedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var webhookEvent in pendingEvents)
        {
            try
            {
                await ProcessEventAsync(webhookEvent, cancellationToken);
                webhookEvent.ProcessedAt = DateTimeOffset.UtcNow;
                webhookEvent.LastError = null;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                webhookEvent.LastError = exception.GetType().Name;
                logger.LogWarning(exception, "Could not process Asaas event {EventId} ({EventType}).", webhookEvent.AsaasEventId, webhookEvent.EventType);
            }

            webhookEvent.Attempts++;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await SchedulePendingInvoicesAsync(cancellationToken);
    }

    private async Task ProcessEventAsync(WebhookEvent webhookEvent, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(webhookEvent.Payload);
        var root = document.RootElement;

        if (webhookEvent.EventType.StartsWith("PAYMENT_", StringComparison.Ordinal))
        {
            await ProcessPaymentEventAsync(root, webhookEvent.EventType, cancellationToken);
        }
        else if (webhookEvent.EventType.StartsWith("INVOICE_", StringComparison.Ordinal))
        {
            await ProcessInvoiceEventAsync(root, webhookEvent.EventType, cancellationToken);
        }
    }

    private async Task ProcessPaymentEventAsync(JsonElement root, string eventType, CancellationToken cancellationToken)
    {
        var paymentData = root.GetProperty("payment");
        var asaasPaymentId = paymentData.GetProperty("id").GetString();
        if (string.IsNullOrWhiteSpace(asaasPaymentId))
        {
            throw new InvalidDataException("Asaas payment event has no payment ID.");
        }

        var payment = await dbContext.Payments
            .FirstOrDefaultAsync(item => item.AsaasPaymentId == asaasPaymentId, cancellationToken);
        if (payment is null && paymentData.TryGetProperty("externalReference", out var externalReference) &&
            Guid.TryParse(externalReference.GetString(), out var localPaymentId))
        {
            payment = await dbContext.Payments.FirstOrDefaultAsync(item => item.Id == localPaymentId, cancellationToken);
        }

        if (payment is null)
        {
            throw new KeyNotFoundException("The payment referenced by the Asaas event is not stored locally yet.");
        }

        payment.AsaasPaymentId ??= asaasPaymentId;
        if (paymentData.TryGetProperty("status", out var status))
        {
            payment.Status = status.GetString() ?? payment.Status;
        }

        if (paymentData.TryGetProperty("billingType", out var billingType))
        {
            payment.PaymentMethod = billingType.GetString() switch
            {
                "PIX" => PaymentMethod.Pix,
                "BOLETO" => PaymentMethod.Boleto,
                "CREDIT_CARD" => PaymentMethod.CreditCard,
                _ => payment.PaymentMethod
            };
        }

        if (eventType == "PAYMENT_CONFIRMED" && payment.SubscriptionId is null)
        {
            var invoice = await dbContext.Invoices.FirstOrDefaultAsync(item => item.PaymentId == payment.Id, cancellationToken);
            if (invoice is null)
            {
                invoice = new Invoice { PaymentId = payment.Id, Status = "CONFIGURATION_REQUIRED" };
                dbContext.Invoices.Add(invoice);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await TryScheduleInvoiceAsync(invoice, payment, cancellationToken);
        }
    }

    private async Task ProcessInvoiceEventAsync(JsonElement root, string eventType, CancellationToken cancellationToken)
    {
        if (!root.TryGetProperty("invoice", out var invoiceData) ||
            !invoiceData.TryGetProperty("id", out var invoiceIdElement))
        {
            return;
        }

        var asaasInvoiceId = invoiceIdElement.GetString();
        if (string.IsNullOrWhiteSpace(asaasInvoiceId))
        {
            return;
        }

        var invoice = await dbContext.Invoices
            .FirstOrDefaultAsync(item => item.AsaasInvoiceId == asaasInvoiceId, cancellationToken);
        if (invoice is null)
        {
            return;
        }

        invoice.Status = eventType == "INVOICE_AUTHORIZED" ? "AUTHORIZED" : "ERROR";
        invoice.AuthorizedAt = eventType == "INVOICE_AUTHORIZED" ? DateTimeOffset.UtcNow : null;
        if (invoiceData.TryGetProperty("pdfUrl", out var pdfUrl))
        {
            invoice.PdfUrl = pdfUrl.GetString();
        }

        if (invoiceData.TryGetProperty("xmlUrl", out var xmlUrl))
        {
            invoice.XmlUrl = xmlUrl.GetString();
        }

        if (invoiceData.TryGetProperty("statusDescription", out var statusDescription))
        {
            invoice.ErrorMessage = statusDescription.GetString();
        }
    }

    private async Task SchedulePendingInvoicesAsync(CancellationToken cancellationToken)
    {
        var invoices = await dbContext.Invoices
            .Where(item => (item.Status == "CONFIGURATION_REQUIRED" || item.Status == "SCHEDULE_FAILED") &&
                           item.ScheduleAttempts < MaxAttempts)
            .OrderBy(item => item.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        foreach (var invoice in invoices)
        {
            var payment = await dbContext.Payments.FirstOrDefaultAsync(item => item.Id == invoice.PaymentId, cancellationToken);
            if (payment is not null)
            {
                await TryScheduleInvoiceAsync(invoice, payment, cancellationToken);
            }
        }
    }

    private async Task TryScheduleInvoiceAsync(Invoice invoice, Payment payment, CancellationToken cancellationToken)
    {
        if (!asaasClient.CanScheduleInvoice || string.IsNullOrWhiteSpace(payment.AsaasPaymentId))
        {
            invoice.Status = "CONFIGURATION_REQUIRED";
            return;
        }

        invoice.Status = "SCHEDULING";
        invoice.ScheduleAttempts++;
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var result = await asaasClient.ScheduleInvoiceAsync(
                invoice.Id,
                payment.AsaasPaymentId,
                payment.Value,
                cancellationToken);
            invoice.AsaasInvoiceId = result.Id;
            invoice.Status = result.Status;
            invoice.PdfUrl = result.PdfUrl;
            invoice.XmlUrl = result.XmlUrl;
            invoice.ErrorMessage = null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            invoice.Status = "SCHEDULE_FAILED";
            invoice.ErrorMessage = exception.GetType().Name;
            logger.LogWarning(exception, "Could not schedule NFS-e for local payment {PaymentId}.", payment.Id);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}