namespace Innovayse.API.Billing;

using Innovayse.API.Billing.Requests;
using Innovayse.Application.Billing.Commands.RemovePaymentMethod;
using Innovayse.Application.Billing.Commands.SetDefaultPaymentMethod;
using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Billing.Queries.GetMyPaymentMethods;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Saved-card management for the authenticated client's own account.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
/// <remarks>
/// Whose account is always the caller's, taken from the credential -- there is deliberately no
/// route that names a client. Adding a card is not here: Stripe requires collecting card details
/// through its own client-side Elements form (this API never sees a card number), so the portal
/// starts that flow itself rather than through a JSON endpoint.
/// </remarks>
[ApiController]
[Route("api/me/payment-methods")]
[Authorize(Roles = Roles.Client)]
public sealed class MyPaymentMethodsController(IMessageBus bus) : ControllerBase
{
    /// <summary>
    /// Lists the authenticated client's saved cards and bank accounts.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The saved payment methods, empty when none have been added yet.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StripePaymentMethodDto>>> GetMyPaymentMethodsAsync(
        CancellationToken ct)
    {
        var methods = await bus.InvokeAsync<IReadOnlyList<StripePaymentMethodDto>>(
            new GetMyPaymentMethodsQuery(), ct);

        return Ok(methods);
    }

    /// <summary>
    /// Makes a saved payment method the account's default.
    /// </summary>
    /// <param name="id">The Stripe PaymentMethod ID.</param>
    /// <param name="request">Must set <see cref="UpdateMyPaymentMethodRequest.SetAsDefault"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content, or 400 for any update other than setting the default.</returns>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateMyPaymentMethodAsync(
        string id, [FromBody] UpdateMyPaymentMethodRequest request, CancellationToken ct)
    {
        if (!request.SetAsDefault)
        {
            return BadRequest(new { message = "Card details cannot be changed. Remove and add a new card instead." });
        }

        await bus.InvokeAsync(new SetDefaultPaymentMethodCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Removes a saved payment method from the authenticated client's own account.
    /// </summary>
    /// <param name="id">The Stripe PaymentMethod ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> RemoveMyPaymentMethodAsync(string id, CancellationToken ct)
    {
        await bus.InvokeAsync(new RemovePaymentMethodCommand(id), ct);
        return NoContent();
    }
}
