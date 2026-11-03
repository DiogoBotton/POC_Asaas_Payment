using API_Payment_Asaas.Services;
using API_Payment_Asaas.Models;

namespace API_Payment_Asaas.Tests;

public sealed class SubscriptionDueDateCalculatorTests
{
    [Fact]
    public void GetBusinessDate_UsesSaoPauloCalendarDate()
    {
        var result = SubscriptionDueDateCalculator.GetBusinessDate(
            new DateTimeOffset(2026, 11, 3, 2, 30, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 11, 2), result);
    }

    [Theory]
    [InlineData(2026, 11, 3, 6, 2026, 11, 6)]
    [InlineData(2026, 11, 15, 15, 2026, 12, 15)]
    [InlineData(2026, 12, 26, 25, 2027, 1, 25)]
    public void GetNextDueDate_ReturnsNextStrictlyFutureOccurrence(
        int year,
        int month,
        int day,
        int billingDay,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        var result = SubscriptionDueDateCalculator.GetNextDueDate(new DateOnly(year, month, day), billingDay);

        Assert.Equal(new DateOnly(expectedYear, expectedMonth, expectedDay), result);
    }

    [Fact]
    public void GetFirstDueDate_CreditCardStartsToday()
    {
        var today = new DateOnly(2026, 11, 3);

        var result = SubscriptionDueDateCalculator.GetFirstDueDate(today, PaymentMethod.CreditCard, null);

        Assert.Equal(today, result);
    }

    [Theory]
    [InlineData(PaymentMethod.Pix, 6, 2026, 11, 6)]
    [InlineData(PaymentMethod.Boleto, 15, 2026, 11, 15)]
    public void GetFirstDueDate_OtherPaymentMethodsKeepSelectedBillingDay(
        PaymentMethod paymentMethod,
        int billingDay,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        var result = SubscriptionDueDateCalculator.GetFirstDueDate(
            new DateOnly(2026, 11, 3), paymentMethod, billingDay);

        Assert.Equal(new DateOnly(expectedYear, expectedMonth, expectedDay), result);
    }

    [Fact]
    public void GetFirstDueDate_NonCardRequiresBillingDay()
    {
        Assert.Throws<ArgumentException>(() =>
            SubscriptionDueDateCalculator.GetFirstDueDate(new DateOnly(2026, 11, 3), PaymentMethod.Pix, null));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(31)]
    public void GetNextDueDate_RejectsUnsupportedBillingDay(int billingDay)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SubscriptionDueDateCalculator.GetNextDueDate(new DateOnly(2026, 11, 3), billingDay));
    }
}