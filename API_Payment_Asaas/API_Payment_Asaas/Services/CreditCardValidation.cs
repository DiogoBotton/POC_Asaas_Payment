namespace API_Payment_Asaas.Services;

public sealed record ValidatedCreditCard(CreditCardDetails Card, CreditCardHolderDetails Holder);

public static class CreditCardValidation
{
    public static ValidatedCreditCard Validate(
        CreditCardDetails? card,
        CreditCardHolderDetails? holder)
    {
        if (card is null)
        {
            throw new ArgumentException("Informe os dados do cartão.");
        }

        if (holder is null)
        {
            throw new ArgumentException("Informe os dados do titular do cartão.");
        }

        var number = DigitsOnly(card.Number);
        var ccv = DigitsOnly(card.Ccv);
        if (number.Length is < 12 or > 19 || ccv.Length is < 3 or > 4 ||
            !int.TryParse(card.ExpiryMonth, out var month) || month is < 1 or > 12 ||
            card.ExpiryYear.Length != 4 || !int.TryParse(card.ExpiryYear, out _))
        {
            throw new ArgumentException("Confira número, validade e código de segurança do cartão.");
        }

        if (string.IsNullOrWhiteSpace(card.HolderName) ||
            string.IsNullOrWhiteSpace(holder.Name) ||
            string.IsNullOrWhiteSpace(holder.Email) ||
            string.IsNullOrWhiteSpace(holder.CpfCnpj) ||
            string.IsNullOrWhiteSpace(holder.PostalCode) ||
            string.IsNullOrWhiteSpace(holder.AddressNumber) ||
            string.IsNullOrWhiteSpace(holder.Phone))
        {
            throw new ArgumentException("Preencha os dados obrigatórios do titular do cartão.");
        }

        return new ValidatedCreditCard(card with { Number = number, Ccv = ccv }, holder);
    }

    private static string DigitsOnly(string value) => new(value.Where(char.IsDigit).ToArray());
}