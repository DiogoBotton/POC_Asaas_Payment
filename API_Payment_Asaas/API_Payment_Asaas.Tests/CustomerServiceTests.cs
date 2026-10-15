using System.Net;
using System.Text.Json;
using API_Payment_Asaas.Data;
using API_Payment_Asaas.Models;
using API_Payment_Asaas.Services;
using API_Payment_Asaas.DTOs.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace API_Payment_Asaas.Tests;

public sealed class CustomerServiceTests
{
    [Fact]
    public async Task FindOrCreateAndSyncAsync_ReusesCustomerByCpfAndUpdatesAddressInAsaas()
    {
        var dbOptions = new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new PaymentDbContext(dbOptions);
        var existingCustomer = new Customer
        {
            AsaasCustomerId = "cus_existing",
            Name = "Old Name",
            Email = "old@example.com",
            CpfCnpj = "123.456.789-00"
        };
        dbContext.Customers.Add(existingCustomer);
        await dbContext.SaveChangesAsync();

        string? requestPath = null;
        string? requestMethod = null;
        string? requestBody = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            requestPath = request.RequestUri?.AbsolutePath;
            requestMethod = request.Method.Method;
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }))
        {
            BaseAddress = new Uri("https://api-sandbox.asaas.com/v3/")
        };
        var asaasClient = new AsaasClient(httpClient, Options.Create(new AsaasOptions { ApiKey = "test-key" }));
        var service = new CustomerService(dbContext, asaasClient);
        var profile = new CustomerProfile(
            "Updated Name",
            "updated@example.com",
            "12345678900",
            "11999999999",
            "01310-000",
            "Av. Paulista",
            "150",
            "Sala 201",
            "Bela Vista");

        var match = await service.FindByCpfCnpjAsync("123.456.789-00", CancellationToken.None);
        var customer = await service.FindOrCreateAndSyncAsync(profile, CancellationToken.None);

        Assert.NotNull(match);
        Assert.Equal(existingCustomer.Id, match.Id);
        Assert.Equal(existingCustomer.Id, customer.Id);
        Assert.Equal("01310000", customer.PostalCode);
        Assert.Equal("Av. Paulista", customer.Address);
        Assert.Equal("PUT", requestMethod);
        Assert.Equal("/v3/customers/cus_existing", requestPath);
        using var body = JsonDocument.Parse(requestBody!);
        Assert.Equal("01310-000", body.RootElement.GetProperty("postalCode").GetString());
        Assert.Equal("Updated Name", body.RootElement.GetProperty("name").GetString());
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responseFactory(request, cancellationToken);
    }
}