using API_Payment_Asaas.Models;

namespace API_Payment_Asaas.Services;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<CustomerResponse?> FindByCpfCnpjAsync(string cpfCnpj, CancellationToken cancellationToken);
    Task<Customer> FindOrCreateAndSyncAsync(CustomerProfile profile, CancellationToken cancellationToken);
}

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