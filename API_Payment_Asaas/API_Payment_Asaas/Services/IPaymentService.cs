using System.ComponentModel.DataAnnotations;
using API_Payment_Asaas.Models;

namespace API_Payment_Asaas.Services;

public interface IPaymentService
{
    Task<PaymentResponse> CreateAsync(CreatePaymentRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<PaymentResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<PaymentResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}

public enum CheckoutMode
{
    Hosted,
    Direct
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

    [Range(typeof(decimal), "0.01", "9999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal Value { get; init; }

    [Required]
    public DateOnly DueDate { get; init; }

    [MaxLength(200)]
    public string Description { get; init; } = "Payment";

    public PaymentMethod PaymentMethod { get; init; } = PaymentMethod.Undefined;
    public CheckoutMode CheckoutMode { get; init; } = CheckoutMode.Hosted;
}

/// <summary>Payment details returned to the application client.</summary>
public sealed record PaymentResponse(
    Guid Id,
    decimal Value,
    PaymentMethod PaymentMethod,
    string Status,
    DateOnly DueDate,
    string? InvoiceUrl,
    string? BankSlipUrl,
    string? PixCopyPaste,
    string? PixQrCodeUrl,
    string? InvoiceStatus,
    string? InvoicePdfUrl,
    string? InvoiceXmlUrl,
    DateTimeOffset CreatedAt);