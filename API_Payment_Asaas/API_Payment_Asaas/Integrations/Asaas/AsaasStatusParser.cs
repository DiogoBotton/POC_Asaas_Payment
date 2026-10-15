using API_Payment_Asaas.Models;

namespace API_Payment_Asaas.Integrations.Asaas;

public static class AsaasStatusParser
{
    public static (PaymentStatus Status, string? StatusUnknown) ParsePaymentStatus(string? value) =>
        Parse(value, PaymentStatus.UNKNOWN);

    public static (SubscriptionStatus Status, string? StatusUnknown) ParseSubscriptionStatus(string? value) =>
        Parse(value, SubscriptionStatus.UNKNOWN);

    public static (InvoiceStatus Status, string? StatusUnknown) ParseInvoiceStatus(string? value) =>
        Parse(value, InvoiceStatus.UNKNOWN);

    private static (TStatus Status, string? StatusUnknown) Parse<TStatus>(string? value, TStatus unknown)
        where TStatus : struct, Enum
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            Enum.TryParse<TStatus>(value, ignoreCase: true, out var status) &&
            Enum.IsDefined(status) &&
            Enum.GetName(status) is { } statusName &&
            string.Equals(value, statusName, StringComparison.OrdinalIgnoreCase) &&
            !EqualityComparer<TStatus>.Default.Equals(status, unknown))
        {
            return (status, null);
        }

        return (unknown, value);
    }
}