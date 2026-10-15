using System.ComponentModel.DataAnnotations;

namespace API_Payment_Asaas.DTOs.Subscriptions;

public sealed record UpdateSubscriptionValueRequest
{
    [Range(typeof(decimal), "0.01", "9999999.99", ParseLimitsInInvariantCulture = true)]
    public decimal Value { get; init; }
}