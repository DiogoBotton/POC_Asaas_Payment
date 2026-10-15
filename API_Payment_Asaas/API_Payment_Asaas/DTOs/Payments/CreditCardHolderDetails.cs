namespace API_Payment_Asaas.DTOs.Payments;

public sealed record CreditCardHolderDetails
{
    public required string Name { get; init; }
    public required string Email { get; init; }
    public required string CpfCnpj { get; init; }
    public required string PostalCode { get; init; }
    public required string AddressNumber { get; init; }
    public required string Phone { get; init; }
    public string? Address { get; init; }
    public string? Province { get; init; }
    public string? AddressComplement { get; init; }
    public string? MobilePhone { get; init; }
}