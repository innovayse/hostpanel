using Innovayse.Hostpanel.Application.Auth.Commands.DisableTwoFactor;
using Innovayse.Hostpanel.Application.Auth.Commands.EnableTwoFactor;
using Innovayse.Hostpanel.Application.Auth.Commands.StartTwoFactorSetup;
using Innovayse.Hostpanel.Application.Auth.Common;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Two-factor authentication for the signed-in account.
/// </summary>
/// <remarks>
/// Whose account is always the caller's, taken from the credential. There is deliberately no
/// route that names a user: an endpoint that enrolled or disarmed somebody else's second factor
/// would undo the thing it exists to provide.
///
/// Reading whether it is on has no route here: the client profile already carries the flag,
/// and a second endpoint answering the same question is a second thing to keep true.
/// </remarks>
[Route("api/me/2fa")]
[Authorize(Policy = Policies.Member)]
[EnableRateLimiting(RateLimitPolicies.Auth)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class MyTwoFactorController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public MyTwoFactorController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>
    /// Issues a TOTP secret to enrol an authenticator app with.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>This does not switch two-factor on; <see cref="EnableAsync"/> does.</remarks>
    /// <returns>
    /// 200 with the secret and the <c>otpauth://</c> URI to render as a QR code; 401 without a session;
    /// 409 with <c>TWO_FACTOR_ALREADY_ENABLED</c> when two-factor is already on (switch it off first);
    /// 429 when the auth rate limit is exceeded.
    /// </returns>
    [HttpPost("setup")]
    [ProducesResponseType(typeof(TwoFactorSetupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SetupAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<TwoFactorSetupDto>>(new StartTwoFactorSetupCommand(), ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Switches two-factor authentication on.
    /// </summary>
    /// <param name="command">The code the authenticator app currently shows.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when it is on; 400 with <c>VALIDATION_ERROR</c> or <c>TWO_FACTOR_CODE_INVALID</c> when the
    /// code did not match; 401 without a session; 429 when the auth rate limit is exceeded.
    /// </returns>
    [HttpPost("enable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> EnableAsync([FromBody] EnableTwoFactorCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command, ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// Switches two-factor authentication off.
    /// </summary>
    /// <param name="command">A current code from the authenticator app and the current password.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>
    /// A code and the current password are required, not merely a signed-in session. Switching it
    /// off ends every session the account held, this one included: see
    /// <see cref="Innovayse.Hostpanel.Application.Auth.Commands.DisableTwoFactor.DisableTwoFactorHandler"/>.
    /// </remarks>
    /// <returns>
    /// 204 when it is off; 400 with <c>VALIDATION_ERROR</c>, <c>CURRENT_PASSWORD_INVALID</c> when the
    /// password is missing or wrong, or <c>TWO_FACTOR_CODE_INVALID</c> when the code did not match;
    /// 401 without a session; 429 when the auth rate limit is exceeded.
    /// </returns>
    [HttpPost("disable")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> DisableAsync([FromBody] DisableTwoFactorCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command, ct);
        return HandleResult(outcome);
    }
}
