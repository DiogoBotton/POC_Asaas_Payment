namespace API_Payment_Asaas.DTOs.Customers;

public sealed record CustomerProfile(
    string Name,
    string Email,
    string? CpfCnpj,
    string? Phone,
    string PostalCode,
    string Address,
    string AddressNumber,
    string? AddressComplement,
    string Province);