using API_Payment_Asaas.DTOs.Fiscal;

namespace API_Payment_Asaas.Services;

public interface IFiscalConfigurationService
{
    Task<FiscalConfigurationResponse> GetConfigurationAsync(CancellationToken cancellationToken);
    Task<FiscalConfigurationResponse> SaveConfigurationAsync(UpdateFiscalConfigurationRequest request, CancellationToken cancellationToken);
    Task<MunicipalServicesResponse> GetMunicipalServicesAsync(int offset, int limit, string? description, CancellationToken cancellationToken);
}