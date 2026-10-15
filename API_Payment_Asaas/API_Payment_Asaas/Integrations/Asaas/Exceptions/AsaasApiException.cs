using System.Net;

namespace API_Payment_Asaas.Integrations.Asaas.Exceptions;

public sealed class AsaasApiException(
    HttpStatusCode statusCode,
    IReadOnlyList<string>? errorCodes = null) : Exception("The Asaas API request failed.")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public IReadOnlyList<string> ErrorCodes { get; } = errorCodes ?? [];
}