using API_Payment_Asaas.DTOs.Payments;

namespace API_Payment_Asaas.Services;

public sealed record ValidatedCreditCard(CreditCardDetails Card, CreditCardHolderDetails Holder);