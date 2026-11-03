using API_Payment_Asaas.Models;

namespace API_Payment_Asaas.Services;

public static class SubscriptionDueDateCalculator
{
    private static readonly int[] AllowedBillingDays = [6, 15, 25];

    public static DateOnly GetBusinessDate(DateTimeOffset instant)
    {
        var timeZoneId = OperatingSystem.IsWindows()
            ? "E. South America Standard Time"
            : "America/Sao_Paulo";
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, timeZone).DateTime);
    }

    public static DateOnly GetFirstDueDate(DateOnly today, PaymentMethod paymentMethod, int? billingDay)
    {
        if (paymentMethod == PaymentMethod.CreditCard)
        {
            return today;
        }

        if (billingDay is null)
        {
            throw new ArgumentException("Escolha um dia de vencimento.", nameof(billingDay));
        }

        return GetNextDueDate(today, billingDay.Value);
    }

    public static DateOnly GetNextDueDate(DateOnly today, int billingDay)
    {
        if (!AllowedBillingDays.Contains(billingDay))
        {
            throw new ArgumentOutOfRangeException(nameof(billingDay), "O dia de vencimento deve ser 6, 15 ou 25.");
        }

        var dueDate = new DateOnly(today.Year, today.Month, billingDay);
        return dueDate > today ? dueDate : dueDate.AddMonths(1);
    }
}