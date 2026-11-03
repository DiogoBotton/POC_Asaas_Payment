using API_Payment_Asaas.Data;
using API_Payment_Asaas.DTOs.Customers;
using API_Payment_Asaas.DTOs.Payments;
using API_Payment_Asaas.Integrations.Asaas.Contracts;
using API_Payment_Asaas.Models;
using API_Payment_Asaas.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace API_Payment_Asaas.Tests;

public sealed class WebhookProcessorTests
{
    [Theory]
    [InlineData("INVOICE_CREATED", "SCHEDULED")]
    [InlineData("INVOICE_SYNCHRONIZED", "SCHEDULED")]
    [InlineData("INVOICE_AUTHORIZED", "AUTHORIZED")]
    [InlineData("INVOICE_ERROR", "PROVIDER_SPECIFIC_ERROR")]
    [InlineData("INVOICE_NEW_EVENT", "FUTURE_PROVIDER_STATUS")]
    public async Task ProcessPendingAsync_PreservesProviderStatusSeparatelyFromEvent(
        string eventType,
        string providerStatus)
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new PaymentDbContext(options);
        var invoice = new Invoice
        {
            AsaasInvoiceId = "inv_test",
            PaymentId = Guid.NewGuid(),
            WorkflowStatus = null,
            AsaasStatus = "PREVIOUS_STATUS"
        };
        dbContext.Invoices.Add(invoice);
        dbContext.WebhookEvents.Add(new WebhookEvent
        {
            AsaasEventId = $"evt_{eventType}",
            EventType = eventType,
            Payload = JsonSerializer.Serialize(new
            {
                @event = eventType,
                invoice = new { id = "inv_test", status = providerStatus }
            })
        });
        await dbContext.SaveChangesAsync();

        var processor = new WebhookProcessor(
            dbContext,
            new UnusedAsaasClient(),
            NullLogger<WebhookProcessor>.Instance);

        await processor.ProcessPendingAsync(CancellationToken.None);

        var savedInvoice = await dbContext.Invoices.SingleAsync();
        var savedEvent = await dbContext.WebhookEvents.SingleAsync();
        Assert.Equal(providerStatus, savedInvoice.AsaasStatus);
        Assert.Equal(eventType, savedEvent.EventType);
        Assert.Null(savedInvoice.WorkflowStatus);
    }

    [Fact]
    public async Task ProcessPendingAsync_RetryUsesInvoiceFiscalSnapshotAfterGlobalConfigurationChanges()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new PaymentDbContext(options);
        var customer = new Customer
        {
            AsaasCustomerId = "cus_test",
            Name = "Sandbox User",
            Email = "sandbox@example.com"
        };
        var payment = new Payment
        {
            AsaasPaymentId = "pay_test",
            Customer = customer,
            CustomerId = customer.Id,
            Value = 12.50m,
            PaymentMethod = PaymentMethod.Pix,
            Status = PaymentStatus.CONFIRMED,
            DueDate = new DateOnly(2026, 11, 3)
        };
        dbContext.Payments.Add(payment);
        dbContext.Invoices.Add(new Invoice
        {
            Payment = payment,
            PaymentId = payment.Id,
            WorkflowStatus = InvoiceWorkflowStatus.SCHEDULE_FAILED,
            FiscalMunicipalServiceId = "old-service",
            FiscalMunicipalServiceName = "Serviço antigo",
            FiscalServiceDescription = "Descrição original",
            FiscalObservations = "Observações originais",
            FiscalRetainIss = true,
            FiscalIss = 4m,
            FiscalPis = 1m,
            FiscalCofins = 2m,
            FiscalCsll = 3m,
            FiscalInss = 0m,
            FiscalIr = 1.5m
        });
        dbContext.FiscalConfigurations.Add(new FiscalConfiguration
        {
            MunicipalServiceId = "new-service",
            MunicipalServiceName = "Serviço novo",
            ServiceDescription = "Descrição nova",
            Observations = "Observações novas",
            Iss = 5m
        });
        await dbContext.SaveChangesAsync();

        var asaasClient = new UnusedAsaasClient();
        var processor = new WebhookProcessor(dbContext, asaasClient, NullLogger<WebhookProcessor>.Instance);

        await processor.ProcessPendingAsync(CancellationToken.None);

        Assert.Equal("old-service", asaasClient.LastInvoiceSettings?.MunicipalServiceId);
        Assert.Equal("Descrição original", asaasClient.LastInvoiceSettings?.ServiceDescription);
        Assert.Equal(4m, asaasClient.LastInvoiceSettings?.Iss);
        Assert.Equal(1m, asaasClient.LastInvoiceSettings?.Pis);
        var savedInvoice = await dbContext.Invoices.SingleAsync();
        Assert.Equal("SCHEDULED", savedInvoice.AsaasStatus);
        Assert.Null(savedInvoice.WorkflowStatus);
    }

    [Fact]
    public async Task ProcessPendingAsync_PreservesUnknownPaymentStatusFromWebhook()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new PaymentDbContext(options);
        var customer = new Customer
        {
            AsaasCustomerId = "cus_test",
            Name = "Sandbox User",
            Email = "sandbox@example.com"
        };
        var payment = new Payment
        {
            AsaasPaymentId = "pay_test",
            Customer = customer,
            CustomerId = customer.Id,
            Value = 12.50m,
            PaymentMethod = PaymentMethod.Undefined,
            Status = PaymentStatus.PENDING,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        dbContext.Payments.Add(payment);
        dbContext.WebhookEvents.Add(new WebhookEvent
        {
            AsaasEventId = "evt_test",
            EventType = "PAYMENT_UPDATED",
            Payload = """{"payment":{"id":"pay_test","status":"NEW_PAYMENT_STATE"}}"""
        });
        await dbContext.SaveChangesAsync();

        var processor = new WebhookProcessor(
            dbContext,
            new UnusedAsaasClient(),
            NullLogger<WebhookProcessor>.Instance);

        await processor.ProcessPendingAsync(CancellationToken.None);
        dbContext.ChangeTracker.Clear();

        var savedPayment = await dbContext.Payments.SingleAsync();
        var savedEvent = await dbContext.WebhookEvents.SingleAsync();
        Assert.Equal(PaymentStatus.UNKNOWN, savedPayment.Status);
        Assert.Equal("NEW_PAYMENT_STATE", savedPayment.StatusUnknown);
        Assert.NotNull(savedEvent.ProcessedAt);
    }

    [Theory]
    [InlineData("PAYMENT_CREDIT_CARD_CAPTURE_REFUSED", "A captura do cartão foi recusada. Verifique com o emissor do cartão.")]
    [InlineData("PAYMENT_REPROVED_BY_RISK_ANALYSIS", "Pagamento reprovado pela análise de risco da Asaas.")]
    public async Task ProcessPendingAsync_PersistsPaymentFailureDescription(string eventType, string expectedMessage)
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new PaymentDbContext(options);
        var customer = new Customer { AsaasCustomerId = "cus_test", Name = "Sandbox User", Email = "sandbox@example.com" };
        var payment = new Payment
        {
            AsaasPaymentId = "pay_test",
            Customer = customer,
            CustomerId = customer.Id,
            Value = 12.50m,
            PaymentMethod = PaymentMethod.CreditCard,
            Status = PaymentStatus.PENDING,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        dbContext.Payments.Add(payment);
        dbContext.WebhookEvents.Add(new WebhookEvent
        {
            AsaasEventId = $"evt_{eventType}",
            EventType = eventType,
            Payload = JsonSerializer.Serialize(new
            {
                payment = new { id = "pay_test", status = "PENDING" }
            })
        });
        await dbContext.SaveChangesAsync();

        var processor = new WebhookProcessor(
            dbContext,
            new UnusedAsaasClient(),
            NullLogger<WebhookProcessor>.Instance);

        await processor.ProcessPendingAsync(CancellationToken.None);

        var savedPayment = await dbContext.Payments.SingleAsync();
        Assert.Equal(PaymentStatus.PENDING, savedPayment.Status);
        Assert.Equal(expectedMessage, savedPayment.ErrorMessage);
    }

    [Fact]
    public async Task ProcessPendingAsync_MapsRiskAnalysisEventWithoutMarkingPaymentAsPaid()
    {
        var options = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new PaymentDbContext(options);
        var customer = new Customer { AsaasCustomerId = "cus_test", Name = "Sandbox User", Email = "sandbox@example.com" };
        dbContext.Payments.Add(new Payment
        {
            AsaasPaymentId = "pay_test",
            Customer = customer,
            CustomerId = customer.Id,
            Value = 12.50m,
            PaymentMethod = PaymentMethod.CreditCard,
            Status = PaymentStatus.PENDING,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow)
        });
        dbContext.WebhookEvents.Add(new WebhookEvent
        {
            AsaasEventId = "evt_risk_analysis",
            EventType = "PAYMENT_AWAITING_RISK_ANALYSIS",
            Payload = """{"payment":{"id":"pay_test","status":"PENDING"}}"""
        });
        await dbContext.SaveChangesAsync();

        var processor = new WebhookProcessor(
            dbContext,
            new UnusedAsaasClient(),
            NullLogger<WebhookProcessor>.Instance);

        await processor.ProcessPendingAsync(CancellationToken.None);

        var savedPayment = await dbContext.Payments.SingleAsync();
        Assert.Equal(PaymentStatus.AWAITING_RISK_ANALYSIS, savedPayment.Status);
        Assert.Null(savedPayment.ErrorMessage);
    }

    private sealed class UnusedAsaasClient : IAsaasClient
    {
        public AsaasInvoiceSettings? LastInvoiceSettings { get; private set; }
        public Task<AsaasMunicipalServicesResult> GetMunicipalServicesAsync(int offset, int limit, string? description, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AsaasCustomerResult> CreateCustomerAsync(CustomerProfile customer, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateCustomerAsync(string customerId, CustomerProfile customer, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AsaasTokenizedCardResult> TokenizeCardAsync(string customerId, CreditCardDetails card, CreditCardHolderDetails holder, string remoteIp, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AsaasPaymentResult> CreateDirectPaymentAsync(Guid localPaymentId, string customerId, decimal value, DateOnly dueDate, string description, PaymentMethod paymentMethod, CreditCardDetails? creditCard, CreditCardHolderDetails? creditCardHolderInfo, string? creditCardToken, string? remoteIp, CreditCardDeviceInfo? deviceInfo, bool enableThreeDSecure, string threeDSecureCallbackUrl, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AsaasSubscriptionResult> CreateSubscriptionAsync(Guid localSubscriptionId, string customerId, decimal value, BillingCycle cycle, DateOnly nextDueDate, PaymentMethod paymentMethod, string description, CreditCardDetails? creditCard, CreditCardHolderDetails? creditCardHolderInfo, string? creditCardToken, string? remoteIp, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateSubscriptionValueAsync(string subscriptionId, decimal value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task InactivateSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeletePaymentAsync(string paymentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AsaasPixResult> GetPixQrCodeAsync(string paymentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string?> GetBoletoIdentificationFieldAsync(string paymentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AsaasInvoiceResult> ScheduleInvoiceAsync(Guid localInvoiceId, string paymentId, decimal value, AsaasInvoiceSettings settings, CancellationToken cancellationToken)
        {
            LastInvoiceSettings = settings;
            return Task.FromResult(new AsaasInvoiceResult("inv_test", "SCHEDULED", null, null));
        }
    }
}