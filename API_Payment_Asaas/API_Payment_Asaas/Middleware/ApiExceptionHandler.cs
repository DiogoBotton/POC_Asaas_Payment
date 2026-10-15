using API_Payment_Asaas.Integrations.Asaas.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace API_Payment_Asaas.Middleware;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail) = exception switch
        {
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Não encontrado", "O recurso solicitado não foi encontrado."),
            ArgumentException => (StatusCodes.Status400BadRequest, "Requisição inválida", exception.Message),
            AsaasApiException => (StatusCodes.Status502BadGateway, "Falha no provedor de pagamentos", "A Asaas não conseguiu concluir a operação."),
            InvalidOperationException => (StatusCodes.Status503ServiceUnavailable, "Serviço indisponível", "A operação não pode ser concluída no momento."),
            _ => (StatusCodes.Status500InternalServerError, "Erro no servidor", "Ocorreu um erro inesperado.")
        };

        if (exception is AsaasApiException asaasException)
        {
            logger.LogWarning(
                exception,
                "Request failed with status {StatusCode}; Asaas returned {ProviderStatusCode} with error codes {ProviderErrorCodes}.",
                statusCode,
                (int)asaasException.StatusCode,
                string.Join(",", asaasException.ErrorCodes));
        }
        else
        {
            logger.LogWarning(exception, "Request failed with status {StatusCode}.", statusCode);
        }
        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        }, cancellationToken);

        return true;
    }
}