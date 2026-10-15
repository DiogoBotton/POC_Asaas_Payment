using API_Payment_Asaas.Models;

namespace API_Payment_Asaas.DTOs.Subscriptions;

public sealed record SubscriptionChargeResponse(
    Guid Id,
    decimal Value,
    PaymentMethod PaymentMethod,
    PaymentStatus Status,
    DateOnly DueDate,
    string? BankSlipIdentificationField,
    string? BankSlipUrl,
    string? PixCopyPaste,
    string? PixQrCodeUrl,
    string? StatusUnknown);