using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Queries.GetInstalledVersion;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>Staff endpoint that returns the running Hostpanel version to any signed-in staff role.</summary>
[Route("api/admin/version")]
[Authorize(Policy = Policies.Staff)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class InstalledVersionController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public InstalledVersionController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns the version of the running Hostpanel build and nothing else.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the installed version;
    /// 401 when the caller is not signed in;
    /// 403 when the caller holds no staff role;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(InstalledVersionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<InstalledVersionDto>>(new GetInstalledVersionQuery(), ct);
        return HandleResult(outcome);
    }
}
