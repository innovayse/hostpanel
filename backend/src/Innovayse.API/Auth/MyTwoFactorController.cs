namespace Innovayse.API.Auth;

using Innovayse.API.Auth.Requests;
using Innovayse.API.RateLimiting;
using Innovayse.Application.Auth.Commands.DisableTwoFactor;
using Innovayse.Application.Auth.Commands.EnableTwoFactor;
using Innovayse.Application.Auth.Commands.StartTwoFactorSetup;
using Innovayse.Application.Auth.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;

/// <summary>
/// Two-factor authentication for the signed-in account.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
/// <remarks>
/// Whose account is always the caller's, taken from the credential. There is deliberately no
/// route that names a user: an endpoint that enrolled or disarmed somebody else's second factor
/// would undo the thing it exists to provide.
///
/// Reading whether it is on has no route here — the client profile already carries the flag,
/// and a second endpoint answering the same question is a second thing to keep true.
/// </remarks>
[ApiController]
[Route("api/me/2fa")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class MyTwoFactorController(IMessageBus bus) : ControllerBase
{
    /// <summary>
    /// Issues a TOTP secret to enrol an authenticator app with.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The secret and the <c>otpauth://</c> URI to render as a QR code.</returns>
    /// <remarks>This does not switch two-factor on; <see cref="EnableAsync"/> does.</remarks>
    [HttpPost("setup")]
    public async Task<ActionResult<TwoFactorSetupDto>> SetupAsync(CancellationToken ct)
    {
        var result = await bus.InvokeAsync<TwoFactorSetupDto?>(
            new StartTwoFactorSetupCommand(), ct);

        return result is null ? Unauthorized() : Ok(result);
    }

    /// <summary>
    /// Switches two-factor authentication on.
    /// </summary>
    /// <param name="request">The code the authenticator app currently shows.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 when it is on, 400 when the code did not match.</returns>
    [HttpPost("enable")]
    public async Task<IActionResult> EnableAsync(
        [FromBody] TwoFactorCodeRequest request, CancellationToken ct)
    {
        var enabled = await bus.InvokeAsync<bool>(
            new EnableTwoFactorCommand(request.Code), ct);

        return enabled ? NoContent() : BadRequest(new { message = "That code did not match." });
    }

    /// <summary>
    /// Switches two-factor authentication off.
    /// </summary>
    /// <param name="request">A current code from the authenticator app.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 when it is off, 400 when the code did not match.</returns>
    /// <remarks>
    /// A code is required, not merely a signed-in session — see
    /// <see cref="DisableTwoFactorHandler"/>.
    /// </remarks>
    [HttpPost("disable")]
    public async Task<IActionResult> DisableAsync(
        [FromBody] TwoFactorCodeRequest request, CancellationToken ct)
    {
        var disabled = await bus.InvokeAsync<bool>(
            new DisableTwoFactorCommand(request.Code), ct);

        return disabled ? NoContent() : BadRequest(new { message = "That code did not match." });
    }
}
