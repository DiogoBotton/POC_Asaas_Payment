using System.ComponentModel.DataAnnotations;

namespace API_Payment_Asaas.DTOs.Payments;

public sealed record CreatePaymentRequest
{
    [Required, MaxLength(200)]
    public required string Name { get; init; }

    [Required, EmailAddress, MaxLength(320)]
    public required string Email { get; init; }

    [MaxLength(20)]
    public string? CpfCnpj { get; init; }

    [MaxLength(30)]
    public string? Phone { get; init; }

    [Required, RegularExpression(@"^\d{5}-?\d{3}$", ErrorMessage = "Informe um CEP válido.")]
    public required string PostalCode { get; init; }

    [Required, MaxLength(255)]
    public required string Address { get; init; }

    [Required, MaxLength(20)]
    public required string AddressNumber { get; init; }

    [MaxLength(255)]
    public string? AddressComplement { get; init; }

    [Required, MaxLength(100)]
    public required string Province { get; init; }

    [Range(typeof(decimal), "0.01", "9999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal Value { get; init; }

    [Required]
    public API_Payment_Asaas.Models.PaymentMethod? PaymentMethod { get; init; }

    public bool SaveCardForFutureUse { get; init; }

    [Required]
    public DateOnly DueDate { get; init; }

    [MaxLength(200)]
    public string Description { get; init; } = "Payment";

    public CreditCardDetails? CreditCard { get; init; }

    public CreditCardHolderDetails? CreditCardHolderInfo { get; init; }

    public CreditCardDeviceInfo? DeviceInfo { get; init; }
}