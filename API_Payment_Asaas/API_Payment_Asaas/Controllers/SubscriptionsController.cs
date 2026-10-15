using API_Payment_Asaas.DTOs.Subscriptions;
using API_Payment_Asaas.Services;
using Microsoft.AspNetCore.Mvc;

namespace API_Payment_Asaas.Controllers;

[ApiController]
[Route("api/subscriptions")]
public sealed class SubscriptionsController(ISubscriptionService subscriptionService) : ControllerBase
{
    /// <summary>Creates an Asaas subscription directly with its selected payment method.</summary>
    [HttpPost]
    [ProducesResponseType<SubscriptionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SubscriptionResponse>> Create(
        CreateSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var subscription = await subscriptionService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = subscription.Id }, subscription);
    }

    /// <summary>Lists subscriptions and their generated cycle payments.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SubscriptionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SubscriptionResponse>>> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await subscriptionService.GetAllAsync(cancellationToken));
    }

    /// <summary>Gets a subscription and its generated payments.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<SubscriptionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionService.GetByIdAsync(id, cancellationToken);
        return subscription is null ? NotFound() : Ok(subscription);
    }

    /// <summary>Changes the value for this customer without changing already generated payments.</summary>
    [HttpPut("{id:guid}/value")]
    [ProducesResponseType<SubscriptionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionResponse>> UpdateValue(
        Guid id,
        UpdateSubscriptionValueRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await subscriptionService.UpdateValueAsync(id, request, cancellationToken));
    }

    /// <summary>Inactivates the recurrence and removes pending payments after the current cycle.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        await subscriptionService.CancelAsync(id, cancellationToken);
        return NoContent();
    }
}