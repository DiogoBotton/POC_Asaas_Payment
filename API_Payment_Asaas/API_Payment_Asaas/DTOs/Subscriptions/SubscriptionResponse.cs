using API_Payment_Asaas.Models;

namespace API_Payment_Asaas.DTOs.Subscriptions;

public sealed record SubscriptionResponse(
    Guid Id,
    string CustomerName,
    string CustomerEmail,
    decimal Value,
    PaymentMethod PaymentMethod,
    BillingCycle Cycle,
    SubscriptionStatus Status,
    DateOnly NextDueDate,
    DateTimeOffset CreatedAt,
    IReadOnlyList<SubscriptionChargeResponse> Payments,
    string? StatusUnknown);