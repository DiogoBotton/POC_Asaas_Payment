namespace API_Payment_Asaas.Services;

public sealed class AsaasOptions
{
    public const string SectionName = "Asaas";

    public string BaseUrl { get; init; } = "https://sandbox.asaas.com/api/v3/";
    public string? ApiKey { get; init; }
    public string? WebhookToken { get; init; }
    public bool EnableThreeDSecure { get; init; }
    public string FrontendBaseUrl { get; init; } = "http://localhost:5173";
    public AsaasInvoiceOptions Invoice { get; init; } = new();
}

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