using System.ComponentModel.DataAnnotations;
using API_Payment_Asaas.Models;

namespace API_Payment_Asaas.Services;

public interface IPaymentService
{
    Task<PaymentResponse> CreateAsync(CreatePaymentRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<PaymentResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<PaymentResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}

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
    public PaymentMethod? PaymentMethod { get; init; }

    public bool SaveCardForFutureUse { get; init; }

    [Required]
    public DateOnly DueDate { get; init; }

    [MaxLength(200)]
    public string Description { get; init; } = "Payment";

    public CreditCardDetails? CreditCard { get; init; }

    public CreditCardHolderDetails? CreditCardHolderInfo { get; init; }

    public CreditCardDeviceInfo? DeviceInfo { get; init; }

}

public sealed record CreditCardDetails
{
    public required string HolderName { get; init; }
    public required string Number { get; init; }
    public required string ExpiryMonth { get; init; }
    public required string ExpiryYear { get; init; }
    public required string Ccv { get; init; }
}

public sealed record CreditCardHolderDetails
{
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required string CpfCnpj { get; init; }
    public required string PostalCode { get; init; }
    public required string AddressNumber { get; init; }
    public required string Phone { get; init; }
    public string? Address { get; init; }
    public string? Province { get; init; }
    public string? AddressComplement { get; init; }
    public string? MobilePhone { get; init; }
}

public sealed record CreditCardDeviceInfo
{
    public int ColorDepth { get; init; }
    public int ScreenHeight { get; init; }
    public int ScreenWidth { get; init; }
    public int TimeZoneOffset { get; init; }
    public required string Language { get; init; }
    public required string UserAgent { get; init; }
}

/// <summary>Payment details returned to the application client.</summary>
public sealed record PaymentResponse(
    Guid Id,
    decimal Value,
    PaymentMethod PaymentMethod,
    string Status,
    DateOnly DueDate,
    string? BankSlipUrl,
    string? BankSlipIdentificationField,
    string? PixCopyPaste,
    string? PixQrCodeUrl,
    string? ThreeDSecureChallengeUrl,
    string? InvoiceStatus,
    string? InvoicePdfUrl,
    string? InvoiceXmlUrl,
    DateTimeOffset CreatedAt);