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