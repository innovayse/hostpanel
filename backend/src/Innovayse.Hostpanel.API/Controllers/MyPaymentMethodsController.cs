using Innovayse.Hostpanel.API.Authorization;
using Innovayse.Hostpanel.API.Extensions;
using Innovayse.Hostpanel.Application.Billing.Commands.BeginMyCardSave;
using Innovayse.Hostpanel.Application.Billing.Commands.CompleteMyCardSave;
using Innovayse.Hostpanel.Application.Billing.Commands.RemovePaymentMethod;
using Innovayse.Hostpanel.Application.Billing.Commands.RevokeMyCardConsents;
using Innovayse.Hostpanel.Application.Billing.Commands.SetDefaultPaymentMethod;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetMyPaymentMethods;
using Innovayse.Hostpanel.Application.Billing.Queries.ListSavedMethodModules;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Saved-method management for the authenticated client's own account.
/// Every action behind the client portal policy may also answer 503 with <c>IDENTITY_PLUGIN_UNAVAILABLE</c> (and <c>Retry-After</c>)
/// when the enabled identity plugin cannot say which account the caller acts for; the convention
/// <c>IdentityPluginUnavailableConvention</c> declares it once in the generated document.
/// </summary>
/// <remarks>
/// Whose account is always the caller's, taken from the credential: there is deliberately no
/// route that names a client. This API never sees a card number: adding a card starts a save session here,
/// the payer completes it in the gateway's own form or page, and the result is read back from the gateway
/// before the card counts as saved. Which gateway holds a method is part of every route and body, because a
/// client can use more than one.
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

    /// <summary>Lists the payment gateway modules the client can save a payment method with right now.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the modules, empty when no gateway offers saved methods; 401 without a session; 403 with <c>CLIENT_PERMISSION_DENIED</c>; 429 when the rate limit is hit.</returns>
    [HttpGet("modules")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewPayInvoices)]
    [ProducesResponseType(typeof(IReadOnlyList<SavedMethodModuleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListSavedMethodModulesAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<SavedMethodModuleDto>>>(new ListSavedMethodModulesQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>Lists the authenticated client's saved payment methods at every gateway they have an account with.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the saved payment methods, empty when none have been added yet or no gateway offers saved methods; 401 without a session;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewPayInvoices</c>, or for a
    /// caller who is not a client; 404 with <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller has no
    /// client record;
    /// 502 with <c>PAYMENT_METHOD_UPSTREAM_FAILED</c> when the gateway refuses the call; 503 with
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
    /// <param name="module">The payment gateway module that holds the method; wins over any module in the body.</param>
    /// <param name="id">The gateway's reference for the method; wins over any id in the body.</param>
    /// <param name="command">Must set <see cref="SetDefaultPaymentMethodCommand.SetAsDefault"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when made the default; 400 with <c>VALIDATION_ERROR</c> when <c>SetAsDefault</c> is not
    /// true, <c>NO_SAVED_PAYMENT_METHODS</c> or <c>PAYMENT_METHOD_NOT_OWNED</c>; 401 without a session; 403 with
    /// <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewPayInvoices</c>, or for a caller who
    /// is not a client; 404 with <c>CLIENT_PROFILE_NOT_FOUND</c>; 409 with <c>PAYMENT_METHOD_UNAVAILABLE</c>
    /// when the gateway module offers no saved methods;
    /// 502 with <c>PAYMENT_METHOD_UPSTREAM_FAILED</c> when the gateway refuses the call; 503 with
    /// <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> when it cannot be reached; 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{module}/{id}")]
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
        string module, string id, [FromBody] SetDefaultPaymentMethodCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { GatewayModuleId = module, PaymentMethodId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Removes a saved payment method from the authenticated client's own account.</summary>
    /// <param name="module">The payment gateway module that holds the method.</param>
    /// <param name="id">The gateway's reference for the method.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when removed; 400 with <c>VALIDATION_ERROR</c>, <c>NO_SAVED_PAYMENT_METHODS</c> or
    /// <c>PAYMENT_METHOD_NOT_OWNED</c>; 401 without a session; 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user
    /// lacks <c>ViewPayInvoices</c>, or for a caller who is not a client; 404 with
    /// <c>CLIENT_PROFILE_NOT_FOUND</c>; 409 with <c>PAYMENT_METHOD_UNAVAILABLE</c> when the gateway module offers no saved methods;
    /// 502 with <c>PAYMENT_METHOD_UPSTREAM_FAILED</c> when the gateway refuses the call; 503 with
    /// <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> when it cannot be reached; 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("{module}/{id}")]
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
    public async Task<IActionResult> RemoveMyPaymentMethodAsync(string module, string id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new RemovePaymentMethodCommand(module, id), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Starts saving a card without a payment: opens a save session at the gateway module and records the caller's
    /// agreement that the card may be charged for renewals without them.
    /// </summary>
    /// <param name="command">Must set <see cref="BeginMyCardSaveCommand.ConsentAccepted"/>; the origin is taken from the request, not the body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the save session (id, mode, and the token or redirect address); 400 with <c>VALIDATION_ERROR</c> when the agreement is not accepted;
    /// 401 without a session; 403 with <c>CLIENT_PERMISSION_DENIED</c>, <c>CLIENT_SUSPENDED</c>, <c>CLIENT_CLOSED</c> or
    /// <c>CLIENT_CARD_PAYMENT_DISABLED</c>; 404 with <c>CLIENT_PROFILE_NOT_FOUND</c>; 409 with <c>PAYMENT_METHOD_UNAVAILABLE</c>
    /// when the gateway module offers no saved methods; 502 with <c>PAYMENT_METHOD_UPSTREAM_FAILED</c>; 503 with
    /// <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c>; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("sessions")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewPayInvoices)]
    [ProducesResponseType(typeof(SavedMethodSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> BeginMyCardSaveAsync([FromBody] BeginMyCardSaveCommand command, CancellationToken ct)
    {
        // The origin is what the server saw, whatever the body says.
        var outcome = await _bus.InvokeAsync<Result<SavedMethodSessionDto>>(command with { Origin = Request.ConsentOrigin() }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Finishes saving a card: the gateway's own record of the save session decides whether it counts.</summary>
    /// <param name="command">The module and save session the previous call answered with.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when the card is saved and the agreement active; 400 with <c>VALIDATION_ERROR</c> or <c>PAYMENT_METHOD_NOT_OWNED</c>;
    /// 401 without a session; 403 with <c>CLIENT_PERMISSION_DENIED</c>; 404 with <c>CLIENT_PROFILE_NOT_FOUND</c> or
    /// <c>CARD_SETUP_NOT_FOUND</c>; 409 with <c>CARD_SETUP_NOT_COMPLETED</c> when the gateway did not save the card, or
    /// <c>PAYMENT_METHOD_UNAVAILABLE</c>; 502 with <c>PAYMENT_METHOD_UPSTREAM_FAILED</c>; 503 with
    /// <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c>; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("sessions/complete")]
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
    public async Task<IActionResult> CompleteMyCardSaveAsync([FromBody] CompleteMyCardSaveCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command, ct);
        return HandleResult(outcome);
    }

    /// <summary>Turns off automatic card charges: no saved card of the account is charged without the client afterwards.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 whether or not an agreement was on; 401 without a session; 403 with <c>CLIENT_PERMISSION_DENIED</c>;
    /// 404 with <c>CLIENT_PROFILE_NOT_FOUND</c>; 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("auto-charge")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewPayInvoices)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TurnOffMyAutoChargeAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new RevokeMyCardConsentsCommand(), ct);
        return HandleResult(outcome);
    }
}
