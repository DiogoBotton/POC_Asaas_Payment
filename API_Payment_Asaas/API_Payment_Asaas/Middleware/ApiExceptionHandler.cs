using System.Net;
using API_Payment_Asaas.Services;
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
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Not found", "The requested resource was not found."),
            ArgumentException => (StatusCodes.Status400BadRequest, "Invalid request", exception.Message),
            AsaasApiException => (StatusCodes.Status502BadGateway, "Payment provider error", "The payment provider could not complete the request."),
            InvalidOperationException => (StatusCodes.Status503ServiceUnavailable, "Service unavailable", "A required integration is not configured."),
            _ => (StatusCodes.Status500InternalServerError, "Server error", "An unexpected error occurred.")
        };

        logger.LogWarning(exception, "Request failed with status {StatusCode}.", statusCode);
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