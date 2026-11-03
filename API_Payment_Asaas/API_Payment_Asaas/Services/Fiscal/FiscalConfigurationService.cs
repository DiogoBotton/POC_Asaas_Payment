using API_Payment_Asaas.Data;
using API_Payment_Asaas.DTOs.Fiscal;
using API_Payment_Asaas.Integrations.Asaas.Contracts;
using API_Payment_Asaas.Models;
using Microsoft.EntityFrameworkCore;

namespace API_Payment_Asaas.Services;

public sealed class FiscalConfigurationService(
    PaymentDbContext dbContext,
    IAsaasClient asaasClient) : IFiscalConfigurationService
{
    public async Task<FiscalConfigurationResponse> GetConfigurationAsync(CancellationToken cancellationToken)
    {
        var configuration = await GetOrCreateConfigurationAsync(cancellationToken);
        return ToResponse(configuration);
    }

    public async Task<FiscalConfigurationResponse> SaveConfigurationAsync(
        UpdateFiscalConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var municipalService = await FindMunicipalServiceAsync(request.MunicipalServiceId, cancellationToken)
            ?? throw new ArgumentException("O serviço municipal selecionado não está disponível no catálogo da Asaas.");
        var configuration = await GetOrCreateConfigurationAsync(cancellationToken);

        configuration.MunicipalServiceId = municipalService.Id;
        configuration.MunicipalServiceName = municipalService.Description;
        configuration.ServiceDescription = request.ServiceDescription.Trim();
        configuration.Observations = request.Observations.Trim();
        configuration.RetainIss = request.RetainIss;
        configuration.Iss = request.Iss;
        configuration.Pis = request.Pis;
        configuration.Cofins = request.Cofins;
        configuration.Csll = request.Csll;
        configuration.Inss = request.Inss;
        configuration.Ir = request.Ir;
        configuration.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(configuration);
    }

    public async Task<MunicipalServicesResponse> GetMunicipalServicesAsync(
        int offset,
        int limit,
        string? description,
        CancellationToken cancellationToken)
    {
        var result = await asaasClient.GetMunicipalServicesAsync(offset, limit, description, cancellationToken);
        return new MunicipalServicesResponse(
            result.Offset,
            result.Limit,
            result.TotalCount,
            result.HasMore,
            result.Data.Select(service => new MunicipalServiceResponse(service.Id, service.Description, service.IssTax)).ToList());
    }

    private async Task<AsaasMunicipalServiceResult?> FindMunicipalServiceAsync(
        string municipalServiceId,
        CancellationToken cancellationToken)
    {
        const int pageSize = 100;
        var offset = 0;
        while (true)
        {
            var page = await asaasClient.GetMunicipalServicesAsync(offset, pageSize, null, cancellationToken);
            var service = page.Data.FirstOrDefault(item => item.Id == municipalServiceId);
            if (service is not null)
            {
                return service;
            }

            if (!page.HasMore || page.Data.Count == 0)
            {
                return null;
            }

            offset += page.Limit > 0 ? page.Limit : pageSize;
        }
    }

    private async Task<FiscalConfiguration> GetOrCreateConfigurationAsync(CancellationToken cancellationToken)
    {
        var configuration = await dbContext.FiscalConfigurations
            .FirstOrDefaultAsync(item => item.Id == FiscalConfiguration.SingletonId, cancellationToken);
        if (configuration is not null)
        {
            return configuration;
        }

        configuration = new FiscalConfiguration();
        dbContext.FiscalConfigurations.Add(configuration);
        await dbContext.SaveChangesAsync(cancellationToken);
        return configuration;
    }

    private static FiscalConfigurationResponse ToResponse(FiscalConfiguration configuration) => new(
        configuration.MunicipalServiceId,
        configuration.MunicipalServiceName,
        configuration.ServiceDescription,
        configuration.Observations,
        configuration.RetainIss,
        configuration.Iss,
        configuration.Pis,
        configuration.Cofins,
        configuration.Csll,
        configuration.Inss,
        configuration.Ir,
        configuration.UpdatedAt);
}