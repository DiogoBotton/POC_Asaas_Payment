using System.Text.Json;
using API_Payment_Asaas.Data;
using API_Payment_Asaas.Integrations.Asaas;
using API_Payment_Asaas.Models;
using Microsoft.EntityFrameworkCore;

namespace API_Payment_Asaas.Tests;

public sealed class AsaasStatusParserTests
{
    [Fact]
    public void ParseStatus_MapsKnownValuesAndPreservesUnknownValues()
    {
        var knownPaymentStatus = AsaasStatusParser.ParsePaymentStatus("received");
        var unknownPaymentStatus = AsaasStatusParser.ParsePaymentStatus("NEW_PAYMENT_STATE");
        var numericPaymentStatus = AsaasStatusParser.ParsePaymentStatus("1");
        var unknownSubscriptionStatus = AsaasStatusParser.ParseSubscriptionStatus("NEW_SUBSCRIPTION_STATE");
        var unknownInvoiceStatus = AsaasStatusParser.ParseInvoiceStatus("NEW_INVOICE_STATE");

        Assert.Equal(PaymentStatus.RECEIVED, knownPaymentStatus.Status);
        Assert.Null(knownPaymentStatus.StatusUnknown);
        Assert.Equal(PaymentStatus.UNKNOWN, unknownPaymentStatus.Status);
        Assert.Equal("NEW_PAYMENT_STATE", unknownPaymentStatus.StatusUnknown);
        Assert.Equal(PaymentStatus.UNKNOWN, numericPaymentStatus.Status);
        Assert.Equal("1", numericPaymentStatus.StatusUnknown);
        Assert.Equal(SubscriptionStatus.UNKNOWN, unknownSubscriptionStatus.Status);
        Assert.Equal("NEW_SUBSCRIPTION_STATE", unknownSubscriptionStatus.StatusUnknown);
        Assert.Equal(InvoiceStatus.UNKNOWN, unknownInvoiceStatus.Status);
        Assert.Equal("NEW_INVOICE_STATE", unknownInvoiceStatus.StatusUnknown);
    }

    [Fact]
    public async Task StatusEnums_UseStringConversionsAndPersistUnknownValues()
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
            Customer = customer,
            CustomerId = customer.Id,
            Value = 12.50m,
            PaymentMethod = PaymentMethod.Pix,
            Status = PaymentStatus.UNKNOWN,
            StatusUnknown = "NEW_PAYMENT_STATE",
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var savedPayment = await dbContext.Payments.SingleAsync();
        var paymentStatusConverter = dbContext.Model.FindEntityType(typeof(Payment))!
            .FindProperty(nameof(Payment.Status))!
            .GetTypeMapping()
            .Converter!;
        var subscriptionStatusConverter = dbContext.Model.FindEntityType(typeof(Subscription))!
            .FindProperty(nameof(Subscription.Status))!
            .GetTypeMapping()
            .Converter!;
        var invoiceStatusConverter = dbContext.Model.FindEntityType(typeof(Invoice))!
            .FindProperty(nameof(Invoice.Status))!
            .GetTypeMapping()
            .Converter!;

        Assert.Equal(PaymentStatus.UNKNOWN, savedPayment.Status);
        Assert.Equal("NEW_PAYMENT_STATE", savedPayment.StatusUnknown);
        Assert.Equal("CREATING", paymentStatusConverter.ConvertToProvider(PaymentStatus.CREATING));
        Assert.Equal("ACTIVE", subscriptionStatusConverter.ConvertToProvider(SubscriptionStatus.ACTIVE));
        Assert.Equal("SCHEDULED", invoiceStatusConverter.ConvertToProvider(InvoiceStatus.SCHEDULED));
        Assert.Equal("\"CREATING\"", JsonSerializer.Serialize(PaymentStatus.CREATING));
    }
}