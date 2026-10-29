using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Queries.GetReleaseStatus;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>Admin endpoint that returns the installed Hostpanel version and the latest known GitHub release.</summary>
[Route("api/admin/release-status")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class ReleaseStatusController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public ReleaseStatusController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>
    /// Returns the installed Hostpanel version, the latest cached GitHub release, and whether
    /// an upgrade is available. The response is built from the database; no outbound HTTP call
    /// is made on this path.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the release status;
    /// 401 when the caller is not signed in;
    /// 403 when the caller's role is not allowed;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(ReleaseStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<ReleaseStatusDto>>(new GetReleaseStatusQuery(), ct);
        return HandleResult(outcome);
    }
}
