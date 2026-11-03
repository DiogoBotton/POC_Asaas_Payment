using System.Net;

namespace API_Payment_Asaas.Integrations.Asaas.Exceptions;

public sealed class AsaasApiException(
    HttpStatusCode statusCode,
    IReadOnlyList<string>? errorCodes = null,
    IReadOnlyList<string>? errorDescriptions = null) : Exception("The Asaas API request failed.")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public IReadOnlyList<string> ErrorCodes { get; } = errorCodes ?? [];
    public IReadOnlyList<string> ErrorDescriptions { get; } = errorDescriptions ?? [];
    public string UserMessage
    {
        get
        {
            var message = ErrorDescriptions.Count > 0
                ? string.Join(" ", ErrorDescriptions)
                : "A Asaas não conseguiu concluir a operação. Consulte os códigos de erro e tente novamente.";
            return message.Length <= 2000 ? message : message[..2000];
        }
    }
}