namespace API_Payment_Asaas.Integrations.Asaas.Contracts;

public sealed record AsaasMunicipalServiceResult(string Id, string Description, decimal? IssTax);

public sealed record AsaasMunicipalServicesResult(
    int Offset,
    int Limit,
    int TotalCount,
    bool HasMore,
    IReadOnlyList<AsaasMunicipalServiceResult> Data);

public sealed record AsaasInvoiceSettings(
    string MunicipalServiceId,
    string MunicipalServiceName,
    string ServiceDescription,
    string Observations,
    bool RetainIss,
    decimal Iss,
    decimal Pis,
    decimal Cofins,
    decimal Csll,
    decimal Inss,
    decimal Ir);