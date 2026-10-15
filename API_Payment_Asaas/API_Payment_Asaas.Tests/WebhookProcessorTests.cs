using API_Payment_Asaas.Data;
using API_Payment_Asaas.DTOs.Customers;
using API_Payment_Asaas.DTOs.Payments;
using API_Payment_Asaas.Integrations.Asaas.Contracts;
using API_Payment_Asaas.Models;
using API_Payment_Asaas.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace API_Payment_Asaas.Tests;

public sealed class WebhookProcessorTests
{
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

    private sealed class UnusedAsaasClient : IAsaasClient
    {
        public bool CanScheduleInvoice => false;
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
        public Task<AsaasInvoiceResult> ScheduleInvoiceAsync(Guid localInvoiceId, string paymentId, decimal value, string? serviceDescription, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}