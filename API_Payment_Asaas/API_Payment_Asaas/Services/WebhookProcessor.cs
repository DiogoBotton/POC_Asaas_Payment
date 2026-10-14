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
        else if (webhookEvent.EventType.StartsWith("SUBSCRIPTION_", StringComparison.Ordinal))
        {
            await ProcessSubscriptionEventAsync(root, webhookEvent.EventType, cancellationToken);
        }
        else if (webhookEvent.EventType.StartsWith("INVOICE_", StringComparison.Ordinal))
        {
            await ProcessInvoiceEventAsync(root, webhookEvent.EventType, cancellationToken);
        }
    }

    private async Task ProcessPaymentEventAsync(JsonElement root, string eventType, CancellationToken cancellationToken)
    {
        if (!root.TryGetProperty("payment", out var paymentData) ||
            !paymentData.TryGetProperty("id", out var paymentIdElement))
        {
            return;
        }

        var asaasPaymentId = paymentIdElement.GetString();
        if (string.IsNullOrWhiteSpace(asaasPaymentId))
        {
            throw new InvalidDataException("Asaas payment event has no payment ID.");
        }

        var localReference = GetString(paymentData, "externalReference");
        var payment = await dbContext.Payments
            .FirstOrDefaultAsync(item => item.AsaasPaymentId == asaasPaymentId, cancellationToken);
        if (payment is null && Guid.TryParse(localReference, out var localPaymentId))
        {
            payment = await dbContext.Payments.FirstOrDefaultAsync(item => item.Id == localPaymentId, cancellationToken);
        }

        var asaasSubscriptionId = GetString(paymentData, "subscription");
        Subscription? subscription = null;
        if (!string.IsNullOrWhiteSpace(asaasSubscriptionId))
        {
            subscription = await dbContext.Subscriptions
                .FirstOrDefaultAsync(item => item.AsaasSubscriptionId == asaasSubscriptionId, cancellationToken);
        }

        if (subscription is null && Guid.TryParse(localReference, out var localSubscriptionId))
        {
            subscription = await dbContext.Subscriptions
                .FirstOrDefaultAsync(item => item.Id == localSubscriptionId, cancellationToken);
        }

        if (payment is null)
        {
            if (subscription is null)
            {
                throw new KeyNotFoundException("The payment or subscription referenced by the Asaas event is not stored locally yet.");
            }

            var dueDate = TryGetDate(paymentData, "dueDate") ?? subscription.NextDueDate;
            var value = TryGetDecimal(paymentData, "value") ?? subscription.Value;
            payment = new Payment
            {
                CustomerId = subscription.CustomerId,
                SubscriptionId = subscription.Id,
                Value = value,
                PaymentMethod = subscription.PaymentMethod,
                Status = "PENDING",
                DueDate = dueDate
            };
            dbContext.Payments.Add(payment);
        }

        payment.AsaasPaymentId ??= asaasPaymentId;
        if (subscription is not null && !string.IsNullOrWhiteSpace(asaasSubscriptionId))
        {
            subscription.AsaasSubscriptionId ??= asaasSubscriptionId;
            payment.SubscriptionId = subscription.Id;
        }

        if (GetString(paymentData, "status") is { } status)
        {
            payment.Status = status;
        }

        if (GetString(paymentData, "billingType") is { } billingType)
        {
            payment.PaymentMethod = billingType switch
            {
                "PIX" => PaymentMethod.Pix,
                "BOLETO" => PaymentMethod.Boleto,
                "CREDIT_CARD" => PaymentMethod.CreditCard,
                _ => payment.PaymentMethod
            };
        }

        payment.Value = TryGetDecimal(paymentData, "value") ?? payment.Value;
        payment.DueDate = TryGetDate(paymentData, "dueDate") ?? payment.DueDate;
        payment.BankSlipUrl = GetString(paymentData, "bankSlipUrl") ?? payment.BankSlipUrl;

        await dbContext.SaveChangesAsync(cancellationToken);

        if (subscription?.Status == "INACTIVE")
        {
            subscription.CancellationCutoffDate ??= subscription.NextDueDate;
            if (payment.DueDate > subscription.CancellationCutoffDate &&
                DeletablePaymentStatuses.Contains(payment.Status) &&
                !string.IsNullOrWhiteSpace(payment.AsaasPaymentId))
            {
                await asaasClient.DeletePaymentAsync(payment.AsaasPaymentId, cancellationToken);
                payment.Status = "DELETED";
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        if (payment.Status != "DELETED" && !string.IsNullOrWhiteSpace(payment.AsaasPaymentId))
        {
            if (payment.PaymentMethod == PaymentMethod.Pix &&
                (string.IsNullOrWhiteSpace(payment.PixCopyPaste) ||
                 string.IsNullOrWhiteSpace(payment.PixQrCodeUrl) || eventType == "PAYMENT_UPDATED"))
            {
                try
                {
                    var pix = await asaasClient.GetPixQrCodeAsync(payment.AsaasPaymentId, cancellationToken);
                    payment.PixCopyPaste = pix.Payload;
                    payment.PixQrCodeUrl = pix.EncodedImage is null ? null : $"data:image/png;base64,{pix.EncodedImage}";
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogWarning(exception, "Payment {PaymentId} was synchronized but Pix instructions could not be retrieved.", payment.Id);
                }
            }
            else if (payment.PaymentMethod == PaymentMethod.Boleto &&
                     (string.IsNullOrWhiteSpace(payment.BankSlipIdentificationField) || eventType == "PAYMENT_UPDATED"))
            {
                try
                {
                    payment.BankSlipIdentificationField = await asaasClient.GetBoletoIdentificationFieldAsync(
                        payment.AsaasPaymentId,
                        cancellationToken);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogWarning(exception, "Payment {PaymentId} was synchronized but its boleto line could not be retrieved.", payment.Id);
                }
            }
        }

        if ((eventType is "PAYMENT_CONFIRMED" or "PAYMENT_RECEIVED") && payment.Status != "DELETED")
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

    private async Task ProcessSubscriptionEventAsync(JsonElement root, string eventType, CancellationToken cancellationToken)
    {
        if (!root.TryGetProperty("subscription", out var subscriptionData))
        {
            return;
        }

        var asaasSubscriptionId = GetString(subscriptionData, "id");
        var subscription = Guid.TryParse(GetString(subscriptionData, "externalReference"), out var localId)
            ? await dbContext.Subscriptions.FirstOrDefaultAsync(item => item.Id == localId, cancellationToken)
            : null;
        subscription ??= !string.IsNullOrWhiteSpace(asaasSubscriptionId)
            ? await dbContext.Subscriptions.FirstOrDefaultAsync(item => item.AsaasSubscriptionId == asaasSubscriptionId, cancellationToken)
            : null;

        if (subscription is null)
        {
            throw new KeyNotFoundException("The subscription referenced by the Asaas event is not stored locally yet.");
        }

        subscription.AsaasSubscriptionId ??= asaasSubscriptionId;
        if (eventType == "SUBSCRIPTION_DELETED")
        {
            subscription.Status = "DELETED";
        }
        else if (eventType == "SUBSCRIPTION_INACTIVATED")
        {
            subscription.Status = "INACTIVE";
        }
        else if (subscription.CanceledAt is null && GetString(subscriptionData, "status") is { } status)
        {
            subscription.Status = status;
        }

        subscription.NextDueDate = TryGetDate(subscriptionData, "nextDueDate") ?? subscription.NextDueDate;
        subscription.Value = TryGetDecimal(subscriptionData, "value") ?? subscription.Value;
        if (GetString(subscriptionData, "cycle") is { } cycle)
        {
            subscription.Cycle = ParseCycle(cycle);
        }
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static decimal? TryGetDecimal(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.TryGetDecimal(out var value)
            ? value
            : null;

    private static DateOnly? TryGetDate(JsonElement element, string propertyName) =>
        DateOnly.TryParse(GetString(element, propertyName), out var date) ? date : null;

    private static BillingCycle ParseCycle(string cycle) => cycle switch
    {
        "WEEKLY" => BillingCycle.Weekly,
        "BIWEEKLY" => BillingCycle.Biweekly,
        "MONTHLY" => BillingCycle.Monthly,
        "BIMONTHLY" => BillingCycle.Bimonthly,
        "QUARTERLY" => BillingCycle.Quarterly,
        "SEMIANNUALLY" => BillingCycle.Semiannually,
        "YEARLY" => BillingCycle.Yearly,
        _ => throw new InvalidDataException("Asaas subscription event contains an unsupported cycle.")
    };

    private static readonly HashSet<string> DeletablePaymentStatuses = ["PENDING", "OVERDUE"];

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
        if (invoice.AsaasInvoiceId is not null ||
            invoice.Status is not ("CONFIGURATION_REQUIRED" or "SCHEDULE_FAILED"))
        {
            return;
        }

        if (!asaasClient.CanScheduleInvoice || string.IsNullOrWhiteSpace(payment.AsaasPaymentId))
        {
            invoice.Status = "CONFIGURATION_REQUIRED";
            invoice.ErrorMessage = "Configure os dados fiscais obrigatórios da NFS-e e confirme o vínculo da cobrança no Asaas.";
            return;
        }

        invoice.Status = "SCHEDULING";
        invoice.ScheduleAttempts++;
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var serviceDescription = payment.SubscriptionId is { } subscriptionId
                ? await dbContext.Subscriptions
                    .Where(item => item.Id == subscriptionId)
                    .Select(item => item.Description)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;
            var result = await asaasClient.ScheduleInvoiceAsync(
                invoice.Id,
                payment.AsaasPaymentId,
                payment.Value,
                serviceDescription,
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
            if (exception is AsaasApiException asaasException)
            {
                var errorCodes = string.Join(",", asaasException.ErrorCodes);
                invoice.ErrorMessage = $"Asaas HTTP {(int)asaasException.StatusCode}: {errorCodes}";
                logger.LogWarning(
                    exception,
                    "Could not schedule NFS-e for local payment {PaymentId}; Asaas returned HTTP {ProviderStatusCode} with error codes {ProviderErrorCodes}.",
                    payment.Id,
                    (int)asaasException.StatusCode,
                    errorCodes);
            }
            else
            {
                invoice.ErrorMessage = exception.GetType().Name;
                logger.LogWarning(exception, "Could not schedule NFS-e for local payment {PaymentId}.", payment.Id);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}