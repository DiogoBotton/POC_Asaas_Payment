namespace API_Payment_Asaas.Models;

public sealed class FiscalConfiguration
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public string? MunicipalServiceId { get; set; }
    public string? MunicipalServiceName { get; set; }
    public string ServiceDescription { get; set; } = string.Empty;
    public string Observations { get; set; } = string.Empty;
    public bool RetainIss { get; set; }
    public decimal Iss { get; set; }
    public decimal Pis { get; set; }
    public decimal Cofins { get; set; }
    public decimal Csll { get; set; }
    public decimal Inss { get; set; }
    public decimal Ir { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}