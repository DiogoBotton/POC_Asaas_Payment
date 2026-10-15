using System.Security.Cryptography;
using System.Text;
using API_Payment_Asaas.Integrations.Asaas.Contracts;
using API_Payment_Asaas.Models;
using Microsoft.AspNetCore.DataProtection;

namespace API_Payment_Asaas.Services;

public sealed class CardTokenProtector : ICardTokenProtector
{
    private readonly IDataProtector _protector;

    public CardTokenProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("API_Payment_Asaas.StoredCardToken.v1");
    }

    public AsaasStoredCard Protect(Guid customerId, AsaasTokenizedCardResult token)
    {
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Token)));
        return new AsaasStoredCard
        {
            CustomerId = customerId,
            ProtectedToken = _protector.Protect(token.Token),
            TokenFingerprint = fingerprint,
            Brand = token.Brand,
            LastFourDigits = token.LastFourDigits
        };
    }

    public string Unprotect(string protectedToken) => _protector.Unprotect(protectedToken);
}