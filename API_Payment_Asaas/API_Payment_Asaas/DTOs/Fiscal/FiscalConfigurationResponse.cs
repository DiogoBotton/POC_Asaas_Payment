namespace API_Payment_Asaas.DTOs.Fiscal;

public sealed record FiscalConfigurationResponse(
    string? MunicipalServiceId,
    string? MunicipalServiceName,
    string ServiceDescription,
    string Observations,
    bool RetainIss,
    decimal Iss,
    decimal Pis,
    decimal Cofins,
    decimal Csll,
    decimal Inss,
    decimal Ir,
    DateTimeOffset UpdatedAt);