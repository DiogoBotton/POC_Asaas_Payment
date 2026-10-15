namespace API_Payment_Asaas.DTOs.Customers;

public sealed record CustomerResponse(
    Guid Id,
    string Name,
    string Email,
    string? CpfCnpj,
    string? Phone,
    string? PostalCode,
    string? Address,
    string? AddressNumber,
    string? AddressComplement,
    string? Province,
    DateTimeOffset CreatedAt);