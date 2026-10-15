namespace API_Payment_Asaas.DTOs.Payments;

public sealed record CreditCardDetails
{
    public required string HolderName { get; init; }
    public required string Number { get; init; }
    public required string ExpiryMonth { get; init; }
    public required string ExpiryYear { get; init; }
    public required string Ccv { get; init; }
}