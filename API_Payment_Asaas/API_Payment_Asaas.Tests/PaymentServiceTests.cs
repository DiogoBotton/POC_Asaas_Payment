using System.Net;
using API_Payment_Asaas.Data;
using API_Payment_Asaas.Models;
using API_Payment_Asaas.Services;
using API_Payment_Asaas.DTOs.Customers;
using API_Payment_Asaas.DTOs.Payments;
using API_Payment_Asaas.DTOs.Subscriptions;
using API_Payment_Asaas.Integrations.Asaas.Contracts;
using API_Payment_Asaas.Integrations.Asaas.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace API_Payment_Asaas.Tests;

public sealed class PaymentServiceTests
{
    [Fact]
    public async Task GetByIdAsync_RetrievesAndPersistsMissingPixInstructions()
    {
        var dbOptions = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new PaymentDbContext(dbOptions);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var asaasClient = new AsaasClientStub();
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
            Status = PaymentStatus.PENDING,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var service = new PaymentService(
            dbContext,
            asaasClient,
            new CustomerService(dbContext, asaasClient),
            new CardTokenProtectorStub(),
            new HttpContextAccessor(),
            Options.Create(new AsaasOptions()),
            NullLogger<PaymentService>.Instance,
            memoryCache);

        var response = await service.GetByIdAsync(payment.Id, CancellationToken.None);
        var savedPayment = await dbContext.Payments.SingleAsync();
        var secondResponse = await service.GetByIdAsync(payment.Id, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal("pix-copy-code", response.PixCopyPaste);
        Assert.Equal("data:image/png;base64,aW1hZ2U=", response.PixQrCodeUrl);
        Assert.Equal("pix-copy-code", savedPayment.PixCopyPaste);
        Assert.NotNull(secondResponse);
        Assert.Equal(1, asaasClient.PixQrCodeRequestCount);
    }

    [Fact]
    public async Task GetByIdAsync_LimitsRepeatedFailedPixInstructionFetches()
    {
        var dbOptions = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new PaymentDbContext(dbOptions);
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var asaasClient = new AsaasClientStub { FailPixRequests = true };
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
            Status = PaymentStatus.PENDING,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync();

        var service = new PaymentService(
            dbContext,
            asaasClient,
            new CustomerService(dbContext, asaasClient),
            new CardTokenProtectorStub(),
            new HttpContextAccessor(),
            Options.Create(new AsaasOptions()),
            NullLogger<PaymentService>.Instance,
            memoryCache);

        await service.GetByIdAsync(payment.Id, CancellationToken.None);
        await service.GetByIdAsync(payment.Id, CancellationToken.None);

        Assert.Equal(1, asaasClient.PixQrCodeRequestCount);
    }

    private sealed class AsaasClientStub : IAsaasClient
    {
        public int PixQrCodeRequestCount { get; private set; }
        public bool FailPixRequests { get; init; }
        public bool CanScheduleInvoice => false;
        public Task<AsaasCustomerResult> CreateCustomerAsync(CustomerProfile customer, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateCustomerAsync(string customerId, CustomerProfile customer, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AsaasTokenizedCardResult> TokenizeCardAsync(string customerId, CreditCardDetails card, CreditCardHolderDetails holder, string remoteIp, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AsaasPaymentResult> CreateDirectPaymentAsync(Guid localPaymentId, string customerId, decimal value, DateOnly dueDate, string description, PaymentMethod paymentMethod, CreditCardDetails? creditCard, CreditCardHolderDetails? creditCardHolderInfo, string? creditCardToken, string? remoteIp, CreditCardDeviceInfo? deviceInfo, bool enableThreeDSecure, string threeDSecureCallbackUrl, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AsaasSubscriptionResult> CreateSubscriptionAsync(Guid localSubscriptionId, string customerId, decimal value, BillingCycle cycle, DateOnly nextDueDate, PaymentMethod paymentMethod, string description, CreditCardDetails? creditCard, CreditCardHolderDetails? creditCardHolderInfo, string? creditCardToken, string? remoteIp, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateSubscriptionValueAsync(string subscriptionId, decimal value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task InactivateSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeletePaymentAsync(string paymentId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AsaasPixResult> GetPixQrCodeAsync(string paymentId, CancellationToken cancellationToken)
        {
            PixQrCodeRequestCount++;
            if (FailPixRequests)
            {
                throw new AsaasApiException(HttpStatusCode.BadGateway);
            }

            return Task.FromResult(new AsaasPixResult("pix-copy-code", "aW1hZ2U="));
        }

        public Task<string?> GetBoletoIdentificationFieldAsync(string paymentId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AsaasInvoiceResult> ScheduleInvoiceAsync(Guid localInvoiceId, string paymentId, decimal value, string? serviceDescription, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class CardTokenProtectorStub : ICardTokenProtector
    {
        public AsaasStoredCard Protect(Guid customerId, AsaasTokenizedCardResult token) => throw new NotSupportedException();
        public string Unprotect(string protectedToken) => throw new NotSupportedException();
    }
}