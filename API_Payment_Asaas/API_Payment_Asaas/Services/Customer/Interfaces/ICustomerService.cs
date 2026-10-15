using API_Payment_Asaas.Models;
using API_Payment_Asaas.DTOs.Customers;

namespace API_Payment_Asaas.Services;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<CustomerResponse?> FindByCpfCnpjAsync(string cpfCnpj, CancellationToken cancellationToken);
    Task<Customer> FindOrCreateAndSyncAsync(CustomerProfile profile, CancellationToken cancellationToken);
}