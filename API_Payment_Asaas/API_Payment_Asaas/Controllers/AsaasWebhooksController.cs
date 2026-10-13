using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using API_Payment_Asaas.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace API_Payment_Asaas.Controllers;

[ApiController]
[Route("api/webhooks/asaas")]
public sealed class AsaasWebhooksController(
    IWebhookInboxService inboxService,
    IOptions<AsaasOptions> options) : ControllerBase
{
    /// <summary>Authenticates and durably queues an Asaas event for asynchronous processing.</summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Receive(
        [FromHeader(Name = "asaas-access-token")] string? accessToken,
        JsonElement body,
        CancellationToken cancellationToken)
    {
        if (!IsAuthorized(accessToken, options.Value.WebhookToken))
        {
            return Unauthorized();
        }

        if (!body.TryGetProperty("id", out var idElement) ||
            !body.TryGetProperty("event", out var eventElement) ||
            string.IsNullOrWhiteSpace(idElement.GetString()) ||
            string.IsNullOrWhiteSpace(eventElement.GetString()))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid webhook payload",
                Detail = "The event ID and type are required."
            });
        }

        var inserted = await inboxService.EnqueueAsync(
            idElement.GetString()!,
            eventElement.GetString()!,
            body.GetRawText(),
            cancellationToken);

        return Ok(new { received = true, duplicate = !inserted });
    }

    private static bool IsAuthorized(string? suppliedToken, string? configuredToken)
    {
        if (string.IsNullOrWhiteSpace(suppliedToken) || string.IsNullOrWhiteSpace(configuredToken))
        {
            return false;
        }

        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedToken);
        var configuredBytes = Encoding.UTF8.GetBytes(configuredToken);
        return suppliedBytes.Length == configuredBytes.Length &&
               CryptographicOperations.FixedTimeEquals(suppliedBytes, configuredBytes);
    }
}