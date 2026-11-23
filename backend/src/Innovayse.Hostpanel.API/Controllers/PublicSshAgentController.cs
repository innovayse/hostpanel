using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Services.Queries.GetSshAgentBundle;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Serves the SSH key agent installer bundle to servers being set up. Anonymous because the server
/// running the one-command installer has no HostPanel credential; safe because the bundle is the
/// same public code for everyone, carries no secret, and the installer verifies its SHA256 against
/// the value the administrator copied from the authenticated admin panel.
/// </summary>
[Route("api/public/ssh-agent")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class PublicSshAgentController : BaseApiController
{
    /// <summary>Wolverine bus the action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initialises the controller.</summary>
    /// <param name="bus">Wolverine bus the action dispatches through.</param>
    public PublicSshAgentController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Downloads an agent bundle this HostPanel build ships: the built in profile alone, or with the profile a plugin ships.</summary>
    /// <param name="query">The profile to install; omitted for the built in profile only.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>200 with the <c>.tar.gz</c>; 404 when no loaded plugin ships the profile; 429 when the rate limit is hit.</returns>
    [HttpGet("bundle.tar.gz")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Upstream)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBundleAsync([FromQuery] GetSshAgentBundleQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<FileDownloadDto>>(query, ct);
        return FileResult(outcome);
    }
}
