using API_Payment_Asaas.Models;

namespace API_Payment_Asaas.Integrations.Asaas.Contracts;

public sealed record AsaasSubscriptionResult(string Id, SubscriptionStatus Status, string? StatusUnknown, DateOnly NextDueDate);