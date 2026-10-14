using System.ComponentModel.DataAnnotations;
using API_Payment_Asaas.Models;

namespace API_Payment_Asaas.Services;

public interface ISubscriptionService
{
    Task<IReadOnlyList<SubscriptionResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<SubscriptionResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<SubscriptionResponse> CreateAsync(CreateSubscriptionRequest request, CancellationToken cancellationToken);
    Task<SubscriptionResponse> UpdateValueAsync(Guid id, UpdateSubscriptionValueRequest request, CancellationToken cancellationToken);
    Task CancelAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record CreateSubscriptionRequest
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

    [Required]
    public BillingCycle? Cycle { get; init; }

    [Required]
    public DateOnly? NextDueDate { get; init; }

    public bool SaveCardForFutureUse { get; init; }

    [MaxLength(150)]
    public string Description { get; init; } = "Assinatura da plataforma";

    public CreditCardDetails? CreditCard { get; init; }

    public CreditCardHolderDetails? CreditCardHolderInfo { get; init; }
}

public sealed record UpdateSubscriptionValueRequest
{
    [Range(typeof(decimal), "0.01", "9999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal Value { get; init; }
}

public sealed record SubscriptionChargeResponse(
    Guid Id,
    decimal Value,
    PaymentMethod PaymentMethod,
    string Status,
    DateOnly DueDate,
    string? BankSlipIdentificationField,
    string? BankSlipUrl,
    string? PixCopyPaste,
    string? PixQrCodeUrl);

public sealed record SubscriptionResponse(
    Guid Id,
    string CustomerName,
    string CustomerEmail,
    decimal Value,
    PaymentMethod PaymentMethod,
    BillingCycle Cycle,
    string Status,
    DateOnly NextDueDate,
    DateTimeOffset CreatedAt,
    IReadOnlyList<SubscriptionChargeResponse> Payments);