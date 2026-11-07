using Innovayse.Hostpanel.API.Authorization;
using Innovayse.Hostpanel.Application.Billing.Commands.BeginSaveStoredMethod;
using Innovayse.Hostpanel.Application.Billing.Commands.CompleteSaveStoredMethod;
using Innovayse.Hostpanel.Application.Billing.Commands.RevokeCardChargeConsent;
using Innovayse.Hostpanel.Application.Billing.Commands.SetDefaultStoredMethod;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.GetMyStoredPaymentMethods;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Stored payment methods of the authenticated client at a payment gateway module that offers the stored credential
/// capability: opening a save session (which records the client's consent to later charges), completing it, and
/// listing the saved methods, making one the default, and revoking the consent. Every action behind the client portal policy may also answer 503 with
/// <c>IDENTITY_PLUGIN_UNAVAILABLE</c> (and <c>Retry-After</c>) when the enabled identity plugin cannot say which
/// account the caller acts for; the convention <c>IdentityPluginUnavailableConvention</c> declares it once in the
/// generated document.
/// </summary>
/// <remarks>
/// Whose account is always the caller's, taken from the credential: there is deliberately no route that names a
/// client. The consent wording and tick box live in the portal; the API refuses a session without the consent.
/// </remarks>
[Route("api/me/stored-payment-methods")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class MyStoredPaymentMethodsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public MyStoredPaymentMethodsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>
    /// Lists the authenticated client's saved payment methods: one entry per active charge consent, with the method's
    /// details read live from the gateway module. A module that is disabled or fails yields its consents with
    /// <c>detailsAvailable</c> false rather than an error.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the saved methods (possibly none); 401 without a session; 403 with <c>CLIENT_PERMISSION_DENIED</c> when
    /// the user lacks <c>ViewPayInvoices</c>; 404 with <c>CLIENT_PROFILE_NOT_FOUND</c>; 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewPayInvoices)]
    [ProducesResponseType(typeof(IReadOnlyList<StoredPaymentMethodDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<StoredPaymentMethodDto>>>(new GetMyStoredPaymentMethodsQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Makes the method of one of the authenticated client's active consents the default: the method the host charges
    /// first, and the one it asks the gateway to treat as the customer's default.
    /// </summary>
    /// <param name="id">The consent id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when the default is set; 400 with <c>VALIDATION_ERROR</c>; 401 without a session; 403 with
    /// <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewPayInvoices</c>; 404 with <c>CARD_CONSENT_NOT_FOUND</c>
    /// when it is not an active consent of the caller, or <c>CLIENT_PROFILE_NOT_FOUND</c>; 409 with
    /// <c>STORED_CREDENTIALS_NOT_SUPPORTED</c> when the module is off or offers no stored credentials; 503 with
    /// <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> when the plugin cannot be reached; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("consents/{id:int}/default")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewPayInvoices)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SetDefaultAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new SetDefaultStoredMethodCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Opens a session in which the payer enters a payment method to store, and records the client's consent to
    /// have it charged later.
    /// </summary>
    /// <param name="command">
    /// The gateway module, the consent flag, the consent wording version and the return address. The address and browser
    /// kept with the consent are the request's own, set here over whatever the body says.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the session (a client token or a redirect address, by mode); 400 with <c>VALIDATION_ERROR</c> when the
    /// consent box is not ticked, or with <c>RETURN_URL_NOT_ALLOWED</c>; 401 without a session; 403 with
    /// <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewPayInvoices</c>, or with <c>CLIENT_SUSPENDED</c>,
    /// <c>CLIENT_CLOSED</c> or <c>CLIENT_CARD_PAYMENT_DISABLED</c> when the account may not save a method; 404 with
    /// <c>CLIENT_PROFILE_NOT_FOUND</c>; 409 with <c>STORED_CREDENTIALS_NOT_SUPPORTED</c> when the module is off or
    /// offers no stored credentials; 503 with <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> when the plugin cannot be
    /// reached; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("sessions")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewPayInvoices)]
    [ProducesResponseType(typeof(SaveMethodSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> BeginSaveAsync([FromBody] BeginSaveStoredMethodCommand command, CancellationToken ct)
    {
        // The consent evidence is the connection's and the headers', never the body's: the overlay wins over any value sent.
        var userAgent = Request.Headers.UserAgent.ToString();
        var outcome = await _bus.InvokeAsync<Result<SaveMethodSessionDto>>(
            command with
            {
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                UserAgent = userAgent.Length > 0 ? userAgent : null,
            },
            ct);
        return HandleResult(outcome);
    }

    /// <summary>Finishes a save session after the payer entered the method, and binds the stored method to the consent.</summary>
    /// <param name="sessionId">The session id; wins over any id in the body.</param>
    /// <param name="command">The gateway module the session was opened at.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the stored method; 400 with <c>VALIDATION_ERROR</c>, <c>CARD_CONSENT_REQUIRED</c> when the consent was
    /// revoked meanwhile, or <c>STORED_METHOD_NOT_SAVED</c> when the payer abandoned the session; 401 without a
    /// session; 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewPayInvoices</c>; 404 with
    /// <c>CARD_CONSENT_NOT_FOUND</c> when the session is not one the caller opened, or <c>CLIENT_PROFILE_NOT_FOUND</c>;
    /// 409 with <c>STORED_CREDENTIALS_NOT_SUPPORTED</c>, or <c>CARD_CONSENT_ALREADY_ATTACHED</c> when the consent already
    /// covers a different method; 503 with <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c> when the
    /// plugin cannot be reached (the same session may be completed again); 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("sessions/{sessionId}/complete")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewPayInvoices)]
    [ProducesResponseType(typeof(StoredMethodDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CompleteSaveAsync(
        string sessionId, [FromBody] CompleteSaveStoredMethodCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<StoredMethodDto>>(command with { SessionId = sessionId }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Revokes one of the authenticated client's stored charge consents; the host never charges that method again.</summary>
    /// <param name="id">The consent id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when revoked, or when it already was; 400 with <c>VALIDATION_ERROR</c>; 401 without a session; 403 with
    /// <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewPayInvoices</c>; 404 with
    /// <c>CARD_CONSENT_NOT_FOUND</c> when it is not the caller's, or <c>CLIENT_PROFILE_NOT_FOUND</c>;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("consents/{id:int}")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewPayInvoices)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeConsentAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new RevokeCardChargeConsentCommand(id), ct);
        return HandleResult(outcome);
    }
}
