using System.Net;
using API_Payment_Asaas.Data;
using API_Payment_Asaas.DTOs.Fiscal;
using API_Payment_Asaas.Models;
using API_Payment_Asaas.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace API_Payment_Asaas.Tests;

public sealed class FiscalConfigurationServiceTests
{
    [Fact]
    public async Task GetConfigurationAsync_CreatesEmptyConfigurationWithZeroRates()
    {
        await using var dbContext = CreateDbContext();
        var service = new FiscalConfigurationService(dbContext, CreateAsaasClient());

        var configuration = await service.GetConfigurationAsync(CancellationToken.None);

        Assert.Null(configuration.MunicipalServiceId);
        Assert.Null(configuration.MunicipalServiceName);
        Assert.Equal(0m, configuration.Iss);
        Assert.Equal(0m, configuration.Pis);
        Assert.Equal(0m, configuration.Cofins);
        Assert.Equal(0m, configuration.Csll);
        Assert.Equal(0m, configuration.Inss);
        Assert.Equal(0m, configuration.Ir);
        Assert.Equal(FiscalConfiguration.SingletonId, await dbContext.FiscalConfigurations.Select(item => item.Id).SingleAsync());
    }

    [Fact]
    public async Task SaveConfigurationAsync_UsesCatalogMetadataAndRejectsUnknownService()
    {
        await using var dbContext = CreateDbContext();
        var service = new FiscalConfigurationService(dbContext, CreateAsaasClient());
        var request = new UpdateFiscalConfigurationRequest
        {
            MunicipalServiceId = "service-15",
            ServiceDescription = "Consultoria mensal",
            Observations = "Referente ao mês vigente",
            RetainIss = true,
            Iss = 2m,
            Pis = 0.65m,
            Cofins = 3m,
            Csll = 1m,
            Inss = 0m,
            Ir = 1.5m
        };

        var saved = await service.SaveConfigurationAsync(request, CancellationToken.None);

        Assert.Equal("service-15", saved.MunicipalServiceId);
        Assert.Equal("15.01 - Consultoria", saved.MunicipalServiceName);
        Assert.Equal(2m, saved.Iss);
        Assert.Equal(0.65m, saved.Pis);

        var invalidRequest = request with { MunicipalServiceId = "not-in-catalog" };
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveConfigurationAsync(invalidRequest, CancellationToken.None));

        var persisted = await dbContext.FiscalConfigurations.SingleAsync();
        Assert.Equal("service-15", persisted.MunicipalServiceId);
        Assert.Equal("15.01 - Consultoria", persisted.MunicipalServiceName);
    }

    private static PaymentDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<PaymentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AsaasClient CreateAsaasClient()
    {
        var httpClient = new HttpClient(new MunicipalServicesHandler())
        {
            BaseAddress = new Uri("https://api-sandbox.asaas.com/v3/")
        };
        return new AsaasClient(httpClient, Options.Create(new AsaasOptions { ApiKey = "test-key" }));
    }

    private sealed class MunicipalServicesHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            const string response = "{\"offset\":0,\"limit\":100,\"totalCount\":1,\"hasMore\":false,\"data\":[{\"id\":\"service-15\",\"description\":\"15.01 - Consultoria\",\"issTax\":5}]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}