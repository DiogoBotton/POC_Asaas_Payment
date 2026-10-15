namespace API_Payment_Asaas.Integrations.Asaas.Contracts;

public sealed record AsaasTokenizedCardResult(string Token, string Brand, string LastFourDigits);