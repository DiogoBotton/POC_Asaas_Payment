using API_Payment_Asaas.Models;

namespace API_Payment_Asaas.DTOs.Payments;

/// <summary>Payment details returned to the application client.</summary>
public sealed record PaymentResponse(
    Guid Id,
    decimal Value,
    PaymentMethod PaymentMethod,
    PaymentStatus Status,
    DateOnly DueDate,
    string? BankSlipUrl,
    string? BankSlipIdentificationField,
    string? PixCopyPaste,
    string? PixQrCodeUrl,
    string? ThreeDSecureChallengeUrl,
    InvoiceStatus? InvoiceStatus,
    string? InvoicePdfUrl,
    string? InvoiceXmlUrl,
    DateTimeOffset CreatedAt,
    string? StatusUnknown,
    string? InvoiceStatusUnknown);