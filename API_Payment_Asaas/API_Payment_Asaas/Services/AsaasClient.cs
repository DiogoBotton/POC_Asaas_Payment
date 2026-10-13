using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using API_Payment_Asaas.Models;
using Microsoft.Extensions.Options;

namespace API_Payment_Asaas.Services;

public sealed class AsaasClient(HttpClient httpClient, IOptions<AsaasOptions> options) : IAsaasClient
{
    private readonly AsaasOptions _options = options.Value;

    public bool CanScheduleInvoice =>
        !string.IsNullOrWhiteSpace(_options.Invoice.ServiceDescription) &&
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
        _options.Invoice.Ir.HasValue;

    public async Task<AsaasCustomerResult> CreateCustomerAsync(
        string name,
        string email,
        string? cpfCnpj,
        string? phone,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var response = await SendAsync<CustomerResponse>(
            httpClient.PostAsJsonAsync("customers", new
            {
                name,
                email,
                cpfCnpj,
                phone
            }, cancellationToken),
            cancellationToken);

        return new AsaasCustomerResult(response.Id);
    }

    public async Task<AsaasPaymentResult> CreatePaymentAsync(
        Guid localPaymentId,
        string customerId,
        decimal value,
        DateOnly dueDate,
        PaymentMethod paymentMethod,
        string description,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var billingType = paymentMethod switch
        {
            PaymentMethod.Undefined => "UNDEFINED",
            PaymentMethod.Pix => "PIX",
            PaymentMethod.Boleto => "BOLETO",
            PaymentMethod.CreditCard => "CREDIT_CARD",
            _ => throw new ArgumentOutOfRangeException(nameof(paymentMethod))
        };

        var response = await SendAsync<PaymentResponse>(
            httpClient.PostAsJsonAsync("payments", new
            {
                customer = customerId,
                billingType,
                value,
                dueDate = dueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                description,
                externalReference = localPaymentId.ToString()
            }, cancellationToken),
            cancellationToken);

        return new AsaasPaymentResult(
            response.Id,
            response.Status,
            response.BillingType,
            DateOnly.ParseExact(response.DueDate, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            response.InvoiceUrl,
            response.BankSlipUrl);
    }

    public async Task<AsaasPixResult> GetPixQrCodeAsync(string paymentId, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        var response = await SendAsync<PixResponse>(
            httpClient.GetAsync($"payments/{Uri.EscapeDataString(paymentId)}/pixQrCode", cancellationToken),
            cancellationToken);

        return new AsaasPixResult(response.Payload, response.EncodedImage, response.ExpirationDate);
    }

    public async Task<AsaasInvoiceResult> ScheduleInvoiceAsync(
        Guid localInvoiceId,
        string paymentId,
        decimal value,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (!CanScheduleInvoice)
        {
            throw new InvalidOperationException("Asaas invoice fiscal settings have not been configured.");
        }

        var invoiceOptions = _options.Invoice;
        var response = await SendAsync<InvoiceResponse>(
            httpClient.PostAsJsonAsync("invoices", new
            {
                payment = paymentId,
                serviceDescription = invoiceOptions.ServiceDescription,
                observations = invoiceOptions.Observations,
                value,
                deductions = 0m,
                effectiveDate = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                municipalServiceId = invoiceOptions.MunicipalServiceId,
                municipalServiceCode = invoiceOptions.MunicipalServiceCode,
                municipalServiceName = invoiceOptions.MunicipalServiceName,
                externalReference = localInvoiceId.ToString(),
                taxes = new
                {
                    retainIss = invoiceOptions.RetainIss,
                    iss = invoiceOptions.Iss,
                    pis = invoiceOptions.Pis,
                    cofins = invoiceOptions.Cofins,
                    csll = invoiceOptions.Csll,
                    inss = invoiceOptions.Inss,
                    ir = invoiceOptions.Ir
                }
            }, cancellationToken),
            cancellationToken);

        return new AsaasInvoiceResult(response.Id, response.Status, response.PdfUrl, response.XmlUrl);
    }

    private async Task<TResponse> SendAsync<TResponse>(Task<HttpResponseMessage> responseTask, CancellationToken cancellationToken)
    {
        using var response = await responseTask;
        if (!response.IsSuccessStatusCode)
        {
            throw new AsaasApiException(response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken)
            ?? throw new AsaasApiException(HttpStatusCode.BadGateway);
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("Asaas API credentials have not been configured.");
        }
    }

    private sealed record CustomerResponse([property: JsonPropertyName("id")] string Id);

    private sealed record PaymentResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("billingType")] string BillingType,
        [property: JsonPropertyName("dueDate")] string DueDate,
        [property: JsonPropertyName("invoiceUrl")] string? InvoiceUrl,
        [property: JsonPropertyName("bankSlipUrl")] string? BankSlipUrl);

    private sealed record PixResponse(
        [property: JsonPropertyName("payload")] string? Payload,
        [property: JsonPropertyName("encodedImage")] string? EncodedImage,
        [property: JsonPropertyName("expirationDate")] DateTimeOffset? ExpirationDate);

    private sealed record InvoiceResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("pdfUrl")] string? PdfUrl,
        [property: JsonPropertyName("xmlUrl")] string? XmlUrl);
}