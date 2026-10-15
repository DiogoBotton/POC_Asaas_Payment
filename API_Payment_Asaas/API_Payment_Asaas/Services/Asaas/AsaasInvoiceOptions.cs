namespace API_Payment_Asaas.Services;

public sealed class AsaasInvoiceOptions
{
    public bool UseFirstMunicipalService { get; init; }
    public string? ServiceDescription { get; init; }
    public string? Observations { get; init; }
    public string? MunicipalServiceId { get; init; }
    public string? MunicipalServiceCode { get; init; }
    public string? MunicipalServiceName { get; init; }
    public bool? RetainIss { get; init; }
    public decimal? Iss { get; init; }
    public decimal? Pis { get; init; }
    public decimal? Cofins { get; init; }
    public decimal? Csll { get; init; }
    public decimal? Inss { get; init; }
    public decimal? Ir { get; init; }
}