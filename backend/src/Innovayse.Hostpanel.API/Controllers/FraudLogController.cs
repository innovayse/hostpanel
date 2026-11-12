using Innovayse.Hostpanel.Application.Fraud.Common;
using Innovayse.Hostpanel.Application.Fraud.Queries.ListFraudLog;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>Admin endpoint for the fraud log: every decision about a registration or an order, with its score and reasons.</summary>
[Route("api/admin/fraud/log")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class FraudLogController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public FraudLogController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns a page of the fraud log, newest first, optionally for one decision and one reason.</summary>
    /// <param name="query">The page, page size and filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the page; 400 with <c>VALIDATION_ERROR</c> for a bad paging window or an unknown decision or reason; 401 without a session;
    /// 403 for a caller who is not an administrator;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<FraudLogEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync([FromQuery] ListFraudLogQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<FraudLogEntryDto>>>(query, ct);
        return HandleResult(outcome);
    }
}
