using Innovayse.Hostpanel.API.Authorization;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Application.Billing.Commands.RemovePaymentMethod;
using Innovayse.Hostpanel.Application.Billing.Commands.SetDefaultPaymentMethod;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetMyPaymentMethods;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Saved-card management for the authenticated client's own account.
/// Every action behind the client portal policy may also answer 503 with <c>IDENTITY_PLUGIN_UNAVAILABLE</c> (and <c>Retry-After</c>)
/// when the enabled identity plugin cannot say which account the caller acts for; the convention
/// <c>IdentityPluginUnavailableConvention</c> declares it once in the generated document.
/// </summary>
/// <remarks>
/// Whose account is always the caller's, taken from the credential: there is deliberately no
/// route that names a client. Adding a card is not here: the card gateway requires collecting card details
/// through its own client-side Elements form (this API never sees a card number), so the portal
/// starts that flow itself rather than through a JSON endpoint.
/// </remarks>
[Route("api/me/payment-methods")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class MyPaymentMethodsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public MyPaymentMethodsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Lists the authenticated client's saved cards and bank accounts.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the saved payment methods, empty when none have been added yet or the card gateway has
    /// no keys; 401 without a session;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewPayInvoices</c>, or for a
    /// caller who is not a client; 404 with <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller has no
    /// client record;
    /// 502 with <c>PAYMENT_METHOD_UPSTREAM_FAILED</c> when the card gateway refuses the call; 503 with
    /// <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> when it cannot be reached; 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewPayInvoices)]
    [ProducesResponseType(typeof(IReadOnlyList<CardPaymentMethodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetMyPaymentMethodsAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<CardPaymentMethodDto>>>(
            new GetMyPaymentMethodsQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>Makes a saved payment method the account's default.</summary>
    /// <param name="id">The card gateway payment method ID; wins over any id in the body.</param>
    /// <param name="command">Must set <see cref="SetDefaultPaymentMethodCommand.SetAsDefault"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when made the default; 400 with <c>VALIDATION_ERROR</c> when <c>SetAsDefault</c> is not
    /// true, <c>NO_SAVED_PAYMENT_METHODS</c> or <c>PAYMENT_METHOD_NOT_OWNED</c>; 401 without a session; 403 with
    /// <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewPayInvoices</c>, or for a caller who
    /// is not a client; 404 with <c>CLIENT_PROFILE_NOT_FOUND</c>; 409 with <c>PAYMENT_METHOD_UNAVAILABLE</c>
    /// when the card gateway has no keys;
    /// 502 with <c>PAYMENT_METHOD_UPSTREAM_FAILED</c> when the card gateway refuses the call; 503 with
    /// <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> when it cannot be reached; 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id}")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewPayInvoices)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UpdateMyPaymentMethodAsync(
        string id, [FromBody] SetDefaultPaymentMethodCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { PaymentMethodId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Removes a saved payment method from the authenticated client's own account.</summary>
    /// <param name="id">The card gateway payment method ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when removed; 400 with <c>VALIDATION_ERROR</c>, <c>NO_SAVED_PAYMENT_METHODS</c> or
    /// <c>PAYMENT_METHOD_NOT_OWNED</c>; 401 without a session; 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user
    /// lacks <c>ViewPayInvoices</c>, or for a caller who is not a client; 404 with
    /// <c>CLIENT_PROFILE_NOT_FOUND</c>; 409 with <c>PAYMENT_METHOD_UNAVAILABLE</c> when the card gateway has no keys;
    /// 502 with <c>PAYMENT_METHOD_UPSTREAM_FAILED</c> when the card gateway refuses the call; 503 with
    /// <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> when it cannot be reached; 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("{id}")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewPayInvoices)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RemoveMyPaymentMethodAsync(string id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new RemovePaymentMethodCommand(id), ct);
        return HandleResult(outcome);
    }
}
