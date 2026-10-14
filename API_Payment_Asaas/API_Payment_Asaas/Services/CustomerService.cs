using API_Payment_Asaas.Data;
using API_Payment_Asaas.Models;
using Microsoft.EntityFrameworkCore;

namespace API_Payment_Asaas.Services;

public sealed class CustomerService(PaymentDbContext dbContext, IAsaasClient asaasClient) : ICustomerService
{
    public async Task<IReadOnlyList<CustomerResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var customers = await dbContext.Customers
            .AsNoTracking()
            .OrderByDescending(customer => customer.CreatedAt)
            .ToListAsync(cancellationToken);

        return customers.Select(ToResponse).ToList();
    }

    public async Task<CustomerResponse?> FindByCpfCnpjAsync(string cpfCnpj, CancellationToken cancellationToken)
    {
        var normalizedDocument = DigitsOnly(cpfCnpj);
        if (normalizedDocument.Length is not (11 or 14))
        {
            return null;
        }

        var customer = await FindByDocumentAsync(normalizedDocument, cancellationToken);
        return customer is null ? null : ToResponse(customer);
    }

    public async Task<Customer> FindOrCreateAndSyncAsync(
        CustomerProfile profile,
        CancellationToken cancellationToken)
    {
        var normalizedProfile = profile with
        {
            Name = profile.Name.Trim(),
            Email = profile.Email.Trim(),
            CpfCnpj = string.IsNullOrWhiteSpace(profile.CpfCnpj) ? null : profile.CpfCnpj.Trim(),
            Phone = string.IsNullOrWhiteSpace(profile.Phone) ? null : profile.Phone.Trim(),
            PostalCode = DigitsOnly(profile.PostalCode),
            Address = profile.Address.Trim(),
            AddressNumber = profile.AddressNumber.Trim(),
            AddressComplement = string.IsNullOrWhiteSpace(profile.AddressComplement) ? null : profile.AddressComplement.Trim(),
            Province = profile.Province.Trim()
        };

        Customer? customer = null;
        var normalizedDocument = DigitsOnly(normalizedProfile.CpfCnpj);
        if (normalizedDocument.Length is 11 or 14)
        {
            customer = await FindByDocumentAsync(normalizedDocument, cancellationToken);
        }

        customer ??= await dbContext.Customers.FirstOrDefaultAsync(
            candidate => candidate.Email.ToLower() == normalizedProfile.Email.ToLower(), cancellationToken);

        if (customer is null)
        {
            var asaasCustomer = await asaasClient.CreateCustomerAsync(normalizedProfile, cancellationToken);
            customer = new Customer { AsaasCustomerId = asaasCustomer.Id, Name = normalizedProfile.Name, Email = normalizedProfile.Email };
            dbContext.Customers.Add(customer);
        }
        else
        {
            await asaasClient.UpdateCustomerAsync(customer.AsaasCustomerId, normalizedProfile, cancellationToken);
        }

        customer.Name = normalizedProfile.Name;
        customer.Email = normalizedProfile.Email;
        customer.CpfCnpj = normalizedProfile.CpfCnpj ?? customer.CpfCnpj;
        customer.Phone = normalizedProfile.Phone ?? customer.Phone;
        customer.PostalCode = normalizedProfile.PostalCode;
        customer.Address = normalizedProfile.Address;
        customer.AddressNumber = normalizedProfile.AddressNumber;
        customer.AddressComplement = normalizedProfile.AddressComplement;
        customer.Province = normalizedProfile.Province;

        await dbContext.SaveChangesAsync(cancellationToken);
        return customer;
    }

    private Task<Customer?> FindByDocumentAsync(string normalizedDocument, CancellationToken cancellationToken) =>
        dbContext.Customers.FirstOrDefaultAsync(
            customer => customer.CpfCnpj != null &&
                        customer.CpfCnpj.Replace(".", "").Replace("-", "").Replace("/", "").Replace(" ", "") == normalizedDocument,
            cancellationToken);

    private static CustomerResponse ToResponse(Customer customer) => new(
        customer.Id,
        customer.Name,
        customer.Email,
        customer.CpfCnpj,
        customer.Phone,
        customer.PostalCode,
        customer.Address,
        customer.AddressNumber,
        customer.AddressComplement,
        customer.Province,
        customer.CreatedAt);

    private static string DigitsOnly(string? value) =>
        value is null ? string.Empty : new(value.Where(char.IsAsciiDigit).ToArray());
}