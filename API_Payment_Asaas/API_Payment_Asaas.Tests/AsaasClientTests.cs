using System.Net;
using System.Text.Json;
using API_Payment_Asaas.Services;
using API_Payment_Asaas.DTOs.Customers;
using API_Payment_Asaas.DTOs.Payments;
using API_Payment_Asaas.Integrations.Asaas.Contracts;
using API_Payment_Asaas.Integrations.Asaas.Exceptions;
using API_Payment_Asaas.Models;
using Microsoft.Extensions.Options;

namespace API_Payment_Asaas.Tests;

public sealed class AsaasClientTests
{
    [Fact]
    public async Task GetMunicipalServicesAsync_ReturnsPagedCatalogAndEscapesDescription()
    {
        string? requestPath = null;
        using var httpClient = CreateHttpClient(async (request, cancellationToken) =>
        {
            requestPath = request.RequestUri!.PathAndQuery;
            await Task.CompletedTask;
            return JsonResponse(HttpStatusCode.OK, "{\"offset\":20,\"limit\":20,\"totalCount\":45,\"hasMore\":true,\"data\":[{\"id\":\"3544\",\"description\":\"01.01.01 - Análise e desenvolvimento de sistemas\",\"issTax\":5}]}");
        });
        var client = new AsaasClient(httpClient, Options.Create(new AsaasOptions { ApiKey = "test-key" }));

        var services = await client.GetMunicipalServicesAsync(20, 20, "sistemas & suporte", CancellationToken.None);

        Assert.Equal("/v3/fiscalInfo/services?offset=20&limit=20&description=sistemas%20%26%20suporte", requestPath);
        Assert.Equal(45, services.TotalCount);
        Assert.True(services.HasMore);
        Assert.Equal("3544", Assert.Single(services.Data).Id);
        Assert.Equal(5m, services.Data[0].IssTax);
    }

    [Fact]
    public async Task ScheduleInvoiceAsync_SendsProvidedFiscalSnapshotWithoutCatalogLookup()
    {
        string? requestBody = null;
        var requestPaths = new List<string>();
        using var httpClient = CreateHttpClient(async (request, cancellationToken) =>
        {
            requestPaths.Add(request.RequestUri!.PathAndQuery);
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse(HttpStatusCode.OK, "{\"id\":\"inv_test\",\"status\":\"SCHEDULED\"}");
        });
        var client = new AsaasClient(httpClient, Options.Create(new AsaasOptions
        {
            ApiKey = "test-key"
        }));
        var localInvoiceId = Guid.NewGuid();
        var settings = new AsaasInvoiceSettings(
            "3544",
            "01.01.01 - Análise e desenvolvimento de sistemas",
            "Serviços de assinatura",
            "Referente ao ciclo mensal",
            false,
            2m,
            0m,
            0m,
            0m,
            0m,
            0m);

        var invoice = await client.ScheduleInvoiceAsync(
            localInvoiceId,
            "pay_test",
            125m,
            settings,
            CancellationToken.None);

        Assert.Equal("inv_test", invoice.Id);
        Assert.Equal("SCHEDULED", invoice.Status);
        Assert.Equal(["/v3/invoices"], requestPaths);
        using var body = JsonDocument.Parse(requestBody!);
        Assert.Equal("pay_test", body.RootElement.GetProperty("payment").GetString());
        Assert.Equal("Serviços de assinatura", body.RootElement.GetProperty("serviceDescription").GetString());
        Assert.Equal("Referente ao ciclo mensal", body.RootElement.GetProperty("observations").GetString());
        Assert.Equal(125m, body.RootElement.GetProperty("value").GetDecimal());
        Assert.Equal("3544", body.RootElement.GetProperty("municipalServiceId").GetString());
        Assert.Equal("01.01.01 - Análise e desenvolvimento de sistemas", body.RootElement.GetProperty("municipalServiceName").GetString());
        Assert.Equal(localInvoiceId.ToString(), body.RootElement.GetProperty("externalReference").GetString());
        var taxes = body.RootElement.GetProperty("taxes");
        Assert.False(taxes.GetProperty("retainIss").GetBoolean());
        Assert.Equal(2m, taxes.GetProperty("iss").GetDecimal());
        Assert.Equal(0m, taxes.GetProperty("pis").GetDecimal());
    }

    [Fact]
    public async Task CreateCustomerAsync_SendsRequiredUserAgentAndCustomerPayload()
    {
        string? userAgent = null;
        string? requestPath = null;
        string? requestBody = null;
        using var httpClient = CreateHttpClient(async (request, cancellationToken) =>
        {
            userAgent = request.Headers.UserAgent.ToString();
            requestPath = request.RequestUri?.AbsolutePath;
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse(HttpStatusCode.OK, """{"id":"cus_test"}""");
        });
        var client = new AsaasClient(httpClient, Options.Create(new AsaasOptions { ApiKey = "test-key" }));

        var customer = await client.CreateCustomerAsync(
            new CustomerProfile("Sandbox User", "sandbox@example.com", "12345678901", "11999999999", "01310-000", "Av. Paulista", "150", "Sala 201", "Bela Vista"),
            CancellationToken.None);

        Assert.Equal("cus_test", customer.Id);
        Assert.Equal("API-Payment-Asaas/1.0", userAgent);
        Assert.Equal("/v3/customers", requestPath);
        using var body = JsonDocument.Parse(requestBody!);
        Assert.Equal("Sandbox User", body.RootElement.GetProperty("name").GetString());
        Assert.Equal("sandbox@example.com", body.RootElement.GetProperty("email").GetString());
        Assert.Equal("01310-000", body.RootElement.GetProperty("postalCode").GetString());
        Assert.Equal("Av. Paulista", body.RootElement.GetProperty("address").GetString());
        Assert.Equal("150", body.RootElement.GetProperty("addressNumber").GetString());
        Assert.Equal("Sala 201", body.RootElement.GetProperty("complement").GetString());
        Assert.Equal("Bela Vista", body.RootElement.GetProperty("province").GetString());
    }

    [Fact]
    public async Task CreateCustomerAsync_PreservesProviderStatusAndErrorCodes()
    {
        using var httpClient = CreateHttpClient((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.BadRequest,
            """{"errors":[{"code":"user_agent_not_informed","description":"omitted from logs"}]}""")));
        var client = new AsaasClient(httpClient, Options.Create(new AsaasOptions { ApiKey = "test-key" }));

        var exception = await Assert.ThrowsAsync<AsaasApiException>(() =>
            client.CreateCustomerAsync(new CustomerProfile("Sandbox User", "sandbox@example.com", null, null, "01310000", "Av. Paulista", "150", null, "Bela Vista"), CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(["user_agent_not_informed"], exception.ErrorCodes);
        Assert.Equal(["omitted from logs"], exception.ErrorDescriptions);
    }

    [Fact]
    public async Task CreateCustomerAsync_HandlesUnexpectedProviderErrorShape()
    {
        using var httpClient = CreateHttpClient((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.BadGateway,
            "[]")));
        var client = new AsaasClient(httpClient, Options.Create(new AsaasOptions { ApiKey = "test-key" }));

        var exception = await Assert.ThrowsAsync<AsaasApiException>(() =>
            client.CreateCustomerAsync(new CustomerProfile("Sandbox User", "sandbox@example.com", null, null, "01310000", "Av. Paulista", "150", null, "Bela Vista"), CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadGateway, exception.StatusCode);
        Assert.Empty(exception.ErrorCodes);
    }

    [Fact]
    public async Task UpdateCustomerAsync_SendsAddressToExistingAsaasCustomer()
    {
        string? requestPath = null;
        string? requestMethod = null;
        string? requestBody = null;
        using var httpClient = CreateHttpClient(async (request, cancellationToken) =>
        {
            requestPath = request.RequestUri?.AbsolutePath;
            requestMethod = request.Method.Method;
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var client = new AsaasClient(httpClient, Options.Create(new AsaasOptions { ApiKey = "test-key" }));

        await client.UpdateCustomerAsync(
            "cus_existing",
            new CustomerProfile("Sandbox User", "sandbox@example.com", null, null, "01310-000", "Av. Paulista", "150", null, "Bela Vista"),
            CancellationToken.None);

        Assert.Equal("PUT", requestMethod);
        Assert.Equal("/v3/customers/cus_existing", requestPath);
        using var body = JsonDocument.Parse(requestBody!);
        Assert.Equal("01310-000", body.RootElement.GetProperty("postalCode").GetString());
        Assert.Equal("Av. Paulista", body.RootElement.GetProperty("address").GetString());
        Assert.False(body.RootElement.TryGetProperty("cpfCnpj", out _));
    }

    [Fact]
    public async Task GetPixQrCodeAsync_IgnoresExpirationDateWithUnsupportedOffsetFormat()
    {
        using var httpClient = CreateHttpClient((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            """{"payload":"pix-copy-code","encodedImage":"aGVsbG8=","expirationDate":"2026-10-14 23:59:59"}""")));
        var client = new AsaasClient(httpClient, Options.Create(new AsaasOptions { ApiKey = "test-key" }));

        var pix = await client.GetPixQrCodeAsync("pay_test", CancellationToken.None);

        Assert.Equal("pix-copy-code", pix.Payload);
        Assert.Equal("aGVsbG8=", pix.EncodedImage);
    }

    [Fact]
    public async Task CreateDirectPaymentAsync_PreservesUnknownProviderStatus()
    {
        using var httpClient = CreateHttpClient((_, _) => Task.FromResult(JsonResponse(
            HttpStatusCode.OK,
            """{"id":"pay_test","status":"NEW_PAYMENT_STATE","billingType":"PIX","dueDate":"2026-10-16"}""")));
        var client = new AsaasClient(httpClient, Options.Create(new AsaasOptions { ApiKey = "test-key" }));

        var payment = await client.CreateDirectPaymentAsync(
            Guid.NewGuid(),
            "cus_test",
            12.50m,
            new DateOnly(2026, 10, 16),
            "Test payment",
            PaymentMethod.Pix,
            null,
            null,
            null,
            null,
            null,
            false,
            "https://localhost/payments/callback",
            CancellationToken.None);

        Assert.Equal(PaymentStatus.UNKNOWN, payment.Status);
        Assert.Equal("NEW_PAYMENT_STATE", payment.StatusUnknown);
    }

    private static HttpClient CreateHttpClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
    {
        return new HttpClient(new StubHttpMessageHandler(responseFactory))
        {
            BaseAddress = new Uri("https://api-sandbox.asaas.com/v3/")
        };
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) => new(statusCode)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responseFactory(request, cancellationToken);
    }
}