using System.Text.Json;
using API_Payment_Asaas.Data;
using API_Payment_Asaas.Integrations.Asaas;
using API_Payment_Asaas.Integrations.Asaas.Contracts;
using API_Payment_Asaas.Integrations.Asaas.Exceptions;
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
                Status = PaymentStatus.PENDING,
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
            var parsedStatus = AsaasStatusParser.ParsePaymentStatus(status);
            payment.Status = parsedStatus.Status;
            payment.StatusUnknown = parsedStatus.StatusUnknown;
        }

        switch (eventType)
        {
            case "PAYMENT_CREATED":
                payment.ErrorMessage = null;
                break;
            case "PAYMENT_AWAITING_RISK_ANALYSIS":
                payment.Status = PaymentStatus.AWAITING_RISK_ANALYSIS;
                payment.StatusUnknown = null;
                payment.ErrorMessage = null;
                break;
            case "PAYMENT_REPROVED_BY_RISK_ANALYSIS":
                payment.ErrorMessage = GetPaymentErrorDescription(paymentData) ??
                    "Pagamento reprovado pela análise de risco da Asaas.";
                break;
            case "PAYMENT_CREDIT_CARD_CAPTURE_REFUSED":
                payment.ErrorMessage = GetPaymentErrorDescription(paymentData) ??
                    "A captura do cartão foi recusada. Verifique com o emissor do cartão.";
                break;
            case "PAYMENT_APPROVED_BY_RISK_ANALYSIS":
            case "PAYMENT_CONFIRMED":
            case "PAYMENT_RECEIVED":
                payment.ErrorMessage = null;
                break;
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

        if (subscription?.Status == SubscriptionStatus.INACTIVE)
        {
            subscription.CancellationCutoffDate ??= subscription.NextDueDate;
            if (payment.DueDate > subscription.CancellationCutoffDate &&
                DeletablePaymentStatuses.Contains(payment.Status) &&
                !string.IsNullOrWhiteSpace(payment.AsaasPaymentId))
            {
                await asaasClient.DeletePaymentAsync(payment.AsaasPaymentId, cancellationToken);
                payment.Status = PaymentStatus.DELETED;
                payment.StatusUnknown = null;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        if (payment.Status != PaymentStatus.DELETED && !string.IsNullOrWhiteSpace(payment.AsaasPaymentId))
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

        if ((eventType is "PAYMENT_CONFIRMED" or "PAYMENT_RECEIVED") && payment.Status != PaymentStatus.DELETED)
        {
            var invoice = await dbContext.Invoices.FirstOrDefaultAsync(item => item.PaymentId == payment.Id, cancellationToken);
            if (invoice is null)
            {
                invoice = new Invoice { PaymentId = payment.Id, WorkflowStatus = InvoiceWorkflowStatus.CONFIGURATION_REQUIRED };
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
            subscription.Status = SubscriptionStatus.DELETED;
            subscription.StatusUnknown = null;
            subscription.ErrorMessage = null;
        }
        else if (eventType == "SUBSCRIPTION_INACTIVATED")
        {
            subscription.Status = SubscriptionStatus.INACTIVE;
            subscription.StatusUnknown = null;
            subscription.ErrorMessage = null;
        }
        else if (subscription.CanceledAt is null && GetString(subscriptionData, "status") is { } status)
        {
            var parsedStatus = AsaasStatusParser.ParseSubscriptionStatus(status);
            subscription.Status = parsedStatus.Status;
            subscription.StatusUnknown = parsedStatus.StatusUnknown;
            if (parsedStatus.Status != SubscriptionStatus.UNKNOWN)
            {
                subscription.ErrorMessage = null;
            }
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

    private static string? GetPaymentErrorDescription(JsonElement paymentData)
    {
        var description = GetString(paymentData, "statusDescription") ??
                          GetString(paymentData, "refusalReason") ??
                          GetString(paymentData, "errorMessage");
        return string.IsNullOrWhiteSpace(description)
            ? null
            : description.Length <= 2000 ? description : description[..2000];
    }

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

    private static readonly HashSet<PaymentStatus> DeletablePaymentStatuses = [PaymentStatus.PENDING, PaymentStatus.OVERDUE];

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

        var parsedEventType = ParseInvoiceEventType(eventType);
        if (GetString(invoiceData, "status") is { } asaasStatus)
        {
            invoice.AsaasStatus = asaasStatus;
        }

        if (parsedEventType == InvoiceWebhookEventType.Authorized)
        {
            invoice.AuthorizedAt ??= DateTimeOffset.UtcNow;
        }

        if (invoiceData.TryGetProperty("pdfUrl", out var pdfUrl))
        {
            invoice.PdfUrl = pdfUrl.GetString();
        }

        if (invoiceData.TryGetProperty("xmlUrl", out var xmlUrl))
        {
            invoice.XmlUrl = xmlUrl.GetString();
        }

        if (parsedEventType == InvoiceWebhookEventType.Error &&
            invoiceData.TryGetProperty("statusDescription", out var statusDescription))
        {
            invoice.ErrorMessage = statusDescription.GetString();
        }
    }

    private static InvoiceWebhookEventType ParseInvoiceEventType(string eventType) => eventType switch
    {
        "INVOICE_CREATED" => InvoiceWebhookEventType.Created,
        "INVOICE_UPDATED" => InvoiceWebhookEventType.Updated,
        "INVOICE_SYNCHRONIZED" => InvoiceWebhookEventType.Synchronized,
        "INVOICE_AUTHORIZED" => InvoiceWebhookEventType.Authorized,
        "INVOICE_PROCESSING_CANCELLATION" => InvoiceWebhookEventType.ProcessingCancellation,
        "INVOICE_CANCELED" => InvoiceWebhookEventType.Canceled,
        "INVOICE_CANCELLATION_DENIED" => InvoiceWebhookEventType.CancellationDenied,
        "INVOICE_ERROR" => InvoiceWebhookEventType.Error,
        _ => InvoiceWebhookEventType.Unknown
    };

    private enum InvoiceWebhookEventType
    {
        Unknown,
        Created,
        Updated,
        Synchronized,
        Authorized,
        ProcessingCancellation,
        Canceled,
        CancellationDenied,
        Error
    }

    private async Task SchedulePendingInvoicesAsync(CancellationToken cancellationToken)
    {
        var invoices = await dbContext.Invoices
            .Where(item => (item.WorkflowStatus == InvoiceWorkflowStatus.CONFIGURATION_REQUIRED || item.WorkflowStatus == InvoiceWorkflowStatus.SCHEDULE_FAILED) &&
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
            invoice.WorkflowStatus is not (InvoiceWorkflowStatus.CONFIGURATION_REQUIRED or InvoiceWorkflowStatus.SCHEDULE_FAILED))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(payment.AsaasPaymentId))
        {
            invoice.WorkflowStatus = InvoiceWorkflowStatus.CONFIGURATION_REQUIRED;
            invoice.ErrorMessage = "Confirme o vínculo da cobrança no Asaas antes de agendar a NFS-e.";
            return;
        }

        if (string.IsNullOrWhiteSpace(invoice.FiscalMunicipalServiceId))
        {
            var configuration = await dbContext.FiscalConfigurations
                .FirstOrDefaultAsync(item => item.Id == FiscalConfiguration.SingletonId, cancellationToken);
            if (configuration is null || string.IsNullOrWhiteSpace(configuration.MunicipalServiceId))
            {
                invoice.WorkflowStatus = InvoiceWorkflowStatus.CONFIGURATION_REQUIRED;
                invoice.ErrorMessage = "Selecione um serviço municipal e salve a configuração fiscal antes de emitir NFS-e.";
                return;
            }

            invoice.FiscalMunicipalServiceId = configuration.MunicipalServiceId;
            invoice.FiscalMunicipalServiceName = configuration.MunicipalServiceName;
            invoice.FiscalServiceDescription = configuration.ServiceDescription;
            invoice.FiscalObservations = configuration.Observations;
            invoice.FiscalRetainIss = configuration.RetainIss;
            invoice.FiscalIss = configuration.Iss;
            invoice.FiscalPis = configuration.Pis;
            invoice.FiscalCofins = configuration.Cofins;
            invoice.FiscalCsll = configuration.Csll;
            invoice.FiscalInss = configuration.Inss;
            invoice.FiscalIr = configuration.Ir;
        }

        invoice.WorkflowStatus = InvoiceWorkflowStatus.SCHEDULING;
        invoice.ScheduleAttempts++;
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var result = await asaasClient.ScheduleInvoiceAsync(
                invoice.Id,
                payment.AsaasPaymentId,
                payment.Value,
                new AsaasInvoiceSettings(
                    invoice.FiscalMunicipalServiceId!,
                    invoice.FiscalMunicipalServiceName!,
                    invoice.FiscalServiceDescription!,
                    invoice.FiscalObservations ?? string.Empty,
                    invoice.FiscalRetainIss ?? false,
                    invoice.FiscalIss ?? 0m,
                    invoice.FiscalPis ?? 0m,
                    invoice.FiscalCofins ?? 0m,
                    invoice.FiscalCsll ?? 0m,
                    invoice.FiscalInss ?? 0m,
                    invoice.FiscalIr ?? 0m),
                cancellationToken);
            invoice.AsaasInvoiceId = result.Id;
            invoice.AsaasStatus = result.Status;
            invoice.WorkflowStatus = null;
            invoice.PdfUrl = result.PdfUrl;
            invoice.XmlUrl = result.XmlUrl;
            invoice.ErrorMessage = null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            invoice.WorkflowStatus = InvoiceWorkflowStatus.SCHEDULE_FAILED;
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