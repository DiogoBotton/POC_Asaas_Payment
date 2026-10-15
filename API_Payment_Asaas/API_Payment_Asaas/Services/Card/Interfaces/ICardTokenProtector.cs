using API_Payment_Asaas.Integrations.Asaas.Contracts;
using API_Payment_Asaas.Models;

namespace API_Payment_Asaas.Services;

public interface ICardTokenProtector
{
    AsaasStoredCard Protect(Guid customerId, AsaasTokenizedCardResult token);
    string Unprotect(string protectedToken);
}