using System.ComponentModel.DataAnnotations;

namespace API_Payment_Asaas.DTOs.Fiscal;

public sealed record UpdateFiscalConfigurationRequest
{
    [Required, MaxLength(50)]
    public required string MunicipalServiceId { get; init; }

    [Required, MaxLength(500)]
    public required string ServiceDescription { get; init; }

    [MaxLength(2000)]
    public string Observations { get; init; } = string.Empty;

    public bool RetainIss { get; init; }

    [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true)]
    public decimal Iss { get; init; }

    [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true)]
    public decimal Pis { get; init; }

    [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true)]
    public decimal Cofins { get; init; }

    [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true)]
    public decimal Csll { get; init; }

    [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true)]
    public decimal Inss { get; init; }

    [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true)]
    public decimal Ir { get; init; }
}