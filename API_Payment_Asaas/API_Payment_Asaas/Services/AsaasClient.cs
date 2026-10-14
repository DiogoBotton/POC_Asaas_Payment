using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using API_Payment_Asaas.Models;
using Microsoft.Extensions.Options;

namespace API_Payment_Asaas.Services;

public sealed class AsaasClient : IAsaasClient
{
    private readonly HttpClient _httpClient;
    private readonly AsaasOptions _options;

    public AsaasClient(HttpClient httpClient, IOptions<AsaasOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("API-Payment-Asaas/1.0");
    }

    public bool CanScheduleInvoice =>
        CanUseSandboxFiscalDefaults ||
        (!string.IsNullOrWhiteSpace(_options.Invoice.ServiceDescription) &&
         _options.Invoice.Observations is not null &&
         (!string.IsNullOrWhiteSpace(_options.Invoice.MunicipalServiceId) ||
          !string.IsNullOrWhiteSpace(_options.Invoice.MunicipalServiceCode)) &&
         !string.IsNullOrWhiteSpace(_options.Invoice.MunicipalServiceName) &&
         _options.Invoice.RetainIss.HasValue &&
         _options.Invoice.Iss.HasValue &&
         _options.Invoice.Pis.HasValue &&
         _options.Invoice.Cofins.HasValue &&
         _options.Invoice.Csll.HasValue &&
         _options.Invoice.Inss.HasValue &&
         _options.Invoice.Ir.HasValue);

    private bool CanUseSandboxFiscalDefaults =>
        _options.Invoice.UseFirstMunicipalService &&
        Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri) &&
        baseUri.Host.Contains("sandbox", StringComparison.OrdinalIgnoreCase);

    public async Task<AsaasCustomerResult> CreateCustomerAsync(
        CustomerProfile customer,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var response = await SendAsync<CustomerResponse>(
            _httpClient.PostAsJsonAsync("customers", new
            {
                customer.Name,
                customer.Email,
                customer.CpfCnpj,
                customer.Phone,
                address = customer.Address,
                addressNumber = customer.AddressNumber,
                complement = customer.AddressComplement,
                province = customer.Province,
                postalCode = FormatPostalCode(customer.PostalCode)
            }, cancellationToken),
            cancellationToken);

        return new AsaasCustomerResult(response.Id);
    }

    public async Task UpdateCustomerAsync(
        string customerId,
        CustomerProfile customer,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var request = new Dictionary<string, object?>
        {
            ["name"] = customer.Name,
            ["email"] = customer.Email,
            ["address"] = customer.Address,
            ["addressNumber"] = customer.AddressNumber,
            ["province"] = customer.Province,
            ["postalCode"] = FormatPostalCode(customer.PostalCode)
        };
        if (!string.IsNullOrWhiteSpace(customer.CpfCnpj)) request["cpfCnpj"] = customer.CpfCnpj;
        if (!string.IsNullOrWhiteSpace(customer.Phone)) request["phone"] = customer.Phone;
        if (!string.IsNullOrWhiteSpace(customer.AddressComplement)) request["complement"] = customer.AddressComplement;

        await EnsureSuccessAsync(_httpClient.PutAsJsonAsync(
            $"customers/{Uri.EscapeDataString(customerId)}",
            request,
            cancellationToken), cancellationToken);
    }

    public async Task<AsaasTokenizedCardResult> TokenizeCardAsync(
        string customerId,
        CreditCardDetails card,
        CreditCardHolderDetails holder,
        string remoteIp,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var response = await SendAsync<CardTokenResponse>(
            _httpClient.PostAsJsonAsync("creditCard/tokenizeCreditCard", new
            {
                customer = customerId,
                creditCard = new
                {
                    holderName = card.HolderName,
                    number = card.Number,
                    expiryMonth = card.ExpiryMonth,
                    expiryYear = card.ExpiryYear,
                    ccv = card.Ccv
                },
                creditCardHolderInfo = ToAsaasCardHolder(holder),
                remoteIp
            }, cancellationToken),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(response.CreditCardToken) ||
            string.IsNullOrWhiteSpace(response.CreditCardNumber) ||
            string.IsNullOrWhiteSpace(response.CreditCardBrand))
        {
            throw new AsaasApiException(HttpStatusCode.BadGateway);
        }

        return new AsaasTokenizedCardResult(
            response.CreditCardToken,
            response.CreditCardBrand,
            response.CreditCardNumber[^Math.Min(4, response.CreditCardNumber.Length)..]);
    }

    public async Task<AsaasPaymentResult> CreateDirectPaymentAsync(
        Guid localPaymentId,
        string customerId,
        decimal value,
        DateOnly dueDate,
        string description,
        PaymentMethod paymentMethod,
        CreditCardDetails? creditCard,
        CreditCardHolderDetails? creditCardHolderInfo,
        string? creditCardToken,
        string? remoteIp,
        CreditCardDeviceInfo? deviceInfo,
        bool enableThreeDSecure,
        string threeDSecureCallbackUrl,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var billingType = ToAsaasPaymentMethod(paymentMethod);
        var request = new Dictionary<string, object?>
        {
            ["customer"] = customerId,
            ["billingType"] = billingType,
            ["value"] = value,
            ["dueDate"] = dueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["description"] = description,
            ["externalReference"] = localPaymentId.ToString()
        };

        if (paymentMethod == PaymentMethod.CreditCard)
        {
            if (string.IsNullOrWhiteSpace(remoteIp) ||
                (string.IsNullOrWhiteSpace(creditCardToken) && (creditCard is null || creditCardHolderInfo is null)))
            {
                throw new ArgumentException("Os dados do cartão e o IP do pagador são obrigatórios.");
            }

            if (!string.IsNullOrWhiteSpace(creditCardToken))
            {
                request["creditCardToken"] = creditCardToken;
            }
            else
            {
                request["creditCard"] = new
                {
                    holderName = creditCard!.HolderName,
                    number = creditCard.Number,
                    expiryMonth = creditCard.ExpiryMonth,
                    expiryYear = creditCard.ExpiryYear,
                    ccv = creditCard.Ccv
                };
                request["creditCardHolderInfo"] = ToAsaasCardHolder(creditCardHolderInfo!);
            }
            request["remoteIp"] = remoteIp;
            if (enableThreeDSecure)
            {
                if (deviceInfo is null)
                {
                    throw new ArgumentException("Os dados do dispositivo são obrigatórios para autenticação 3DS.");
                }

                request["threeDSecure"] = new { callbackUrl = threeDSecureCallbackUrl };
                request["deviceInfo"] = new
                {
                    colorDepth = deviceInfo.ColorDepth,
                    screenHeight = deviceInfo.ScreenHeight,
                    screenWidth = deviceInfo.ScreenWidth,
                    timeZoneOffset = deviceInfo.TimeZoneOffset,
                    language = deviceInfo.Language,
                    userAgent = deviceInfo.UserAgent,
                    remoteIp
                };
            }
        }

        var response = await SendAsync<PaymentResponse>(
            _httpClient.PostAsJsonAsync("payments", request, cancellationToken),
            cancellationToken);

        return new AsaasPaymentResult(
            response.Id,
            response.Status,
            response.BillingType,
            DateOnly.ParseExact(response.DueDate, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            response.BankSlipUrl,
            response.ThreeDSecureChallengeUrl);
    }

    public async Task<AsaasSubscriptionResult> CreateSubscriptionAsync(
        Guid localSubscriptionId,
        string customerId,
        decimal value,
        BillingCycle cycle,
        DateOnly nextDueDate,
        PaymentMethod paymentMethod,
        string description,
        CreditCardDetails? creditCard,
        CreditCardHolderDetails? creditCardHolderInfo,
        string? creditCardToken,
        string? remoteIp,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (paymentMethod is not (PaymentMethod.CreditCard or PaymentMethod.Pix or PaymentMethod.Boleto))
        {
            throw new ArgumentException("Meio de pagamento inválido para assinatura.", nameof(paymentMethod));
        }

        var request = new Dictionary<string, object?>
        {
            ["customer"] = customerId,
            ["billingType"] = ToAsaasPaymentMethod(paymentMethod),
            ["value"] = value,
            ["nextDueDate"] = nextDueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["cycle"] = ToAsaasCycle(cycle),
            ["description"] = description,
            ["externalReference"] = localSubscriptionId.ToString()
        };

        if (paymentMethod == PaymentMethod.CreditCard &&
            (string.IsNullOrWhiteSpace(remoteIp) ||
             (string.IsNullOrWhiteSpace(creditCardToken) && (creditCard is null || creditCardHolderInfo is null))))
        {
            throw new ArgumentException("Os dados do cartão e o IP do pagador são obrigatórios para recorrência.");
        }

        if (paymentMethod == PaymentMethod.CreditCard)
        {
            request["remoteIp"] = remoteIp;
            if (!string.IsNullOrWhiteSpace(creditCardToken))
            {
                request["creditCardToken"] = creditCardToken;
            }
            else
            {
                request["creditCard"] = new
                {
                    holderName = creditCard!.HolderName,
                    number = creditCard.Number,
                    expiryMonth = creditCard.ExpiryMonth,
                    expiryYear = creditCard.ExpiryYear,
                    ccv = creditCard.Ccv
                };
                request["creditCardHolderInfo"] = ToAsaasCardHolder(creditCardHolderInfo!);
            }
        }

        var response = await SendAsync<SubscriptionResponse>(
            _httpClient.PostAsJsonAsync("subscriptions", request, cancellationToken),
            cancellationToken);

        return new AsaasSubscriptionResult(
            response.Id,
            response.Status,
            DateOnly.ParseExact(response.NextDueDate, "yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    public async Task UpdateSubscriptionValueAsync(string subscriptionId, decimal value, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        await EnsureSuccessAsync(_httpClient.PutAsJsonAsync(
            $"subscriptions/{Uri.EscapeDataString(subscriptionId)}",
            new { value, updatePendingPayments = false },
            cancellationToken), cancellationToken);
    }

    public async Task InactivateSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        await EnsureSuccessAsync(_httpClient.PutAsJsonAsync(
            $"subscriptions/{Uri.EscapeDataString(subscriptionId)}",
            new { status = "INACTIVE" },
            cancellationToken), cancellationToken);
    }

    public async Task DeletePaymentAsync(string paymentId, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        await EnsureSuccessAsync(_httpClient.DeleteAsync(
            $"payments/{Uri.EscapeDataString(paymentId)}",
            cancellationToken), cancellationToken);
    }

    public async Task<AsaasPixResult> GetPixQrCodeAsync(string paymentId, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var response = await SendAsync<PixResponse>(
            _httpClient.GetAsync($"payments/{Uri.EscapeDataString(paymentId)}/pixQrCode", cancellationToken),
            cancellationToken);

        return new AsaasPixResult(response.Payload, response.EncodedImage);
    }

    public async Task<string?> GetBoletoIdentificationFieldAsync(string paymentId, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var response = await SendAsync<BoletoIdentificationResponse>(
            _httpClient.GetAsync($"payments/{Uri.EscapeDataString(paymentId)}/identificationField", cancellationToken),
            cancellationToken);
        return response.IdentificationField;
    }

    public async Task<AsaasInvoiceResult> ScheduleInvoiceAsync(
        Guid localInvoiceId,
        string paymentId,
        decimal value,
        string? serviceDescription,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (!CanScheduleInvoice)
        {
            throw new InvalidOperationException("Configure os dados fiscais obrigatórios da NFS-e antes de agendar a emissão.");
        }

        var invoiceOptions = _options.Invoice;
        var municipalServiceId = invoiceOptions.MunicipalServiceId;
        var municipalServiceCode = invoiceOptions.MunicipalServiceCode;
        var municipalServiceName = invoiceOptions.MunicipalServiceName;
        var iss = invoiceOptions.Iss;
        MunicipalServiceResponse? municipalService = null;
        if (CanUseSandboxFiscalDefaults)
        {
            municipalService = await GetFirstMunicipalServiceAsync(cancellationToken)
                ?? throw new InvalidOperationException("A lista de serviços municipais da Asaas está vazia.");
            municipalServiceId = municipalService.Id;
            municipalServiceCode = null;
            municipalServiceName = municipalService.Description;
            iss = municipalService.IssTax ?? iss ?? 0m;
        }

        if (string.IsNullOrWhiteSpace(municipalServiceId) && string.IsNullOrWhiteSpace(municipalServiceCode))
        {
            throw new InvalidOperationException("Não foi encontrado um serviço municipal único correspondente à descrição configurada.");
        }

        if (!iss.HasValue)
        {
            throw new InvalidOperationException("A alíquota de ISS não foi configurada nem retornada pelo serviço municipal.");
        }

        var effectiveDescription = !string.IsNullOrWhiteSpace(serviceDescription)
            ? serviceDescription.Trim()
            : !string.IsNullOrWhiteSpace(invoiceOptions.ServiceDescription)
                ? invoiceOptions.ServiceDescription
                : municipalService?.Description;
        var invoiceRequest = new Dictionary<string, object?>
        {
            ["payment"] = paymentId,
            ["value"] = value,
            ["deductions"] = 0m,
            ["effectiveDate"] = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["externalReference"] = localInvoiceId.ToString(),
            ["observations"] = invoiceOptions.Observations ?? string.Empty
        };

        AddIfConfigured(invoiceRequest, "serviceDescription", effectiveDescription);
        if (!string.IsNullOrWhiteSpace(municipalServiceId))
        {
            invoiceRequest["municipalServiceId"] = municipalServiceId;
        }
        else
        {
            invoiceRequest["municipalServiceCode"] = municipalServiceCode;
        }
        invoiceRequest["municipalServiceName"] = municipalServiceName;

        var taxes = new Dictionary<string, object?>();
        AddIfConfigured(taxes, "retainIss", invoiceOptions.RetainIss ?? false);
        AddIfConfigured(taxes, "iss", iss);
        AddIfConfigured(taxes, "pis", invoiceOptions.Pis ?? (CanUseSandboxFiscalDefaults ? 0m : null));
        AddIfConfigured(taxes, "cofins", invoiceOptions.Cofins ?? (CanUseSandboxFiscalDefaults ? 0m : null));
        AddIfConfigured(taxes, "csll", invoiceOptions.Csll ?? (CanUseSandboxFiscalDefaults ? 0m : null));
        AddIfConfigured(taxes, "inss", invoiceOptions.Inss ?? (CanUseSandboxFiscalDefaults ? 0m : null));
        AddIfConfigured(taxes, "ir", invoiceOptions.Ir ?? (CanUseSandboxFiscalDefaults ? 0m : null));
        if (taxes.Count > 0)
        {
            invoiceRequest["taxes"] = taxes;
        }

        var response = await SendAsync<InvoiceResponse>(
            _httpClient.PostAsJsonAsync("invoices", invoiceRequest, cancellationToken),
            cancellationToken);

        return new AsaasInvoiceResult(response.Id, response.Status, response.PdfUrl, response.XmlUrl);
    }

    private async Task<MunicipalServiceResponse?> GetFirstMunicipalServiceAsync(CancellationToken cancellationToken)
    {
        var response = await SendAsync<MunicipalServicesResponse>(
            _httpClient.GetAsync("fiscalInfo/services?offset=0&limit=1", cancellationToken),
            cancellationToken);
        return response.Data?.FirstOrDefault();
    }

    private static void AddIfConfigured(IDictionary<string, object?> values, string key, object? value)
    {
        if (value is not null && (value is not string text || !string.IsNullOrWhiteSpace(text)))
        {
            values[key] = value;
        }
    }

    private async Task<TResponse> SendAsync<TResponse>(Task<HttpResponseMessage> responseTask, CancellationToken cancellationToken)
    {
        using var response = await responseTask;
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateApiExceptionAsync(response, cancellationToken);
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken)
            ?? throw new AsaasApiException(HttpStatusCode.BadGateway);
    }

    private static async Task EnsureSuccessAsync(
        Task<HttpResponseMessage> responseTask,
        CancellationToken cancellationToken)
    {
        using var response = await responseTask;
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateApiExceptionAsync(response, cancellationToken);
        }
    }

    private static async Task<AsaasApiException> CreateApiExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var errorCodes = new List<string>();
        try
        {
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(contentStream, cancellationToken: cancellationToken);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Array)
            {
                foreach (var error in errors.EnumerateArray())
                {
                    if (error.ValueKind == JsonValueKind.Object &&
                        error.TryGetProperty("code", out var code) &&
                        code.ValueKind == JsonValueKind.String &&
                        code.GetString() is { Length: <= 100 } errorCode &&
                        errorCode.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.'))
                    {
                        errorCodes.Add(errorCode);
                    }
                }
            }
        }
        catch (JsonException)
        {
        }

        return new AsaasApiException(response.StatusCode, errorCodes);
    }

    private static string ToAsaasCycle(BillingCycle cycle) => cycle switch
    {
        BillingCycle.Weekly => "WEEKLY",
        BillingCycle.Biweekly => "BIWEEKLY",
        BillingCycle.Monthly => "MONTHLY",
        BillingCycle.Bimonthly => "BIMONTHLY",
        BillingCycle.Quarterly => "QUARTERLY",
        BillingCycle.Semiannually => "SEMIANNUALLY",
        BillingCycle.Yearly => "YEARLY",
        _ => throw new ArgumentOutOfRangeException(nameof(cycle))
    };

    private static string ToAsaasPaymentMethod(PaymentMethod paymentMethod) => paymentMethod switch
    {
        PaymentMethod.CreditCard => "CREDIT_CARD",
        PaymentMethod.Pix => "PIX",
        PaymentMethod.Boleto => "BOLETO",
        _ => throw new ArgumentOutOfRangeException(nameof(paymentMethod))
    };

    private static object ToAsaasCardHolder(CreditCardHolderDetails holder) => new
    {
        name = holder.Name,
        email = holder.Email,
        cpfCnpj = holder.CpfCnpj,
        postalCode = holder.PostalCode,
        addressNumber = holder.AddressNumber,
        phone = holder.Phone,
        address = holder.Address,
        province = holder.Province,
        addressComplement = holder.AddressComplement,
        mobilePhone = holder.MobilePhone
    };

    private static string FormatPostalCode(string value)
    {
        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        return digits.Length == 8 ? $"{digits[..5]}-{digits[5..]}" : value;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("As credenciais da API Asaas não foram configuradas.");
        }
    }

    private sealed record CustomerResponse([property: JsonPropertyName("id")] string Id);

    private sealed record CardTokenResponse(
        [property: JsonPropertyName("creditCardToken")] string? CreditCardToken,
        [property: JsonPropertyName("creditCardBrand")] string? CreditCardBrand,
        [property: JsonPropertyName("creditCardNumber")] string? CreditCardNumber);

    private sealed record SubscriptionResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("nextDueDate")] string NextDueDate);

    private sealed record PaymentResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("billingType")] string BillingType,
        [property: JsonPropertyName("dueDate")] string DueDate,
        [property: JsonPropertyName("bankSlipUrl")] string? BankSlipUrl,
        [property: JsonPropertyName("threeDSecureChallengeUrl")] string? ThreeDSecureChallengeUrl);

    private sealed record PixResponse(
        [property: JsonPropertyName("payload")] string? Payload,
        [property: JsonPropertyName("encodedImage")] string? EncodedImage);

    private sealed record BoletoIdentificationResponse(
        [property: JsonPropertyName("identificationField")] string? IdentificationField);

    private sealed record MunicipalServicesResponse(
        [property: JsonPropertyName("totalCount")] int TotalCount,
        [property: JsonPropertyName("data")] IReadOnlyList<MunicipalServiceResponse>? Data);

    private sealed record MunicipalServiceResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("issTax")] decimal? IssTax);

    private sealed record InvoiceResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("pdfUrl")] string? PdfUrl,
        [property: JsonPropertyName("xmlUrl")] string? XmlUrl);
}