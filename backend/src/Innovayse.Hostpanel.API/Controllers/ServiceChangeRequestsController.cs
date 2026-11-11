using Innovayse.Hostpanel.Application.Services.Commands.ApproveServiceChangeRequest;
using Innovayse.Hostpanel.Application.Services.Commands.RejectServiceChangeRequest;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Application.Services.Queries.GetServiceChangeRequests;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>Admin endpoints for the plan change requests of client services: the pending queue, one service's history, approval and rejection.</summary>
[Route("api/admin/service-change-requests")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class ServiceChangeRequestsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public ServiceChangeRequestsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>
    /// Returns a paginated list of plan change requests, newest first: filter on <c>status=Pending</c> for the queue
    /// awaiting staff, or on <c>serviceId</c> for one service's plan change history.
    /// </summary>
    /// <param name="query">The optional service and status filters, the page and the page size.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the page; 400 when the query fails validation; 401 without a session; 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<ServiceChangeRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllAsync([FromQuery] GetServiceChangeRequestsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<ServiceChangeRequestDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Approves a plan change request and applies it; approving an Approved request applies it again.</summary>
    /// <param name="id">Plan change request primary key; wins over any id in the body.</param>
    /// <param name="command">The optional note.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when the service now runs on the new plan; 400 when the body fails validation, or with
    /// <c>SERVICE_NOT_ACTIVE</c> when the service is not active; 401 without a session; 402 with
    /// <c>SERVICE_CHANGE_INVOICE_UNPAID</c> when the upgrade invoice is not paid; 403 for a caller who is not an admin;
    /// 404 with <c>SERVICE_CHANGE_REQUEST_NOT_FOUND</c>; 409 with <c>SERVICE_CHANGE_INVALID_STATE</c> when the request is
    /// Completed or Rejected or the service left its plan, or <c>PRODUCT_PACKAGE_MISSING</c> when the new plan names no
    /// hosting package; 502 with <c>SERVICE_PACKAGE_UPSTREAM_FAILED</c> when the hosting panel refuses the package (the
    /// request stays Approved, so the call can be repeated); 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status402PaymentRequired)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> ApproveAsync(int id, [FromBody] ApproveServiceChangeRequestCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { RequestId = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Rejects a pending or approved plan change request; an unpaid proration invoice is cancelled with it.</summary>
    /// <param name="id">Plan change request primary key; wins over any id in the body.</param>
    /// <param name="command">The optional note.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when rejected; 400 when the body fails validation, or with the invoice refusal when the proration invoice is
    /// already paid; 401 without a session; 403 for a caller who is not an admin; 404 with
    /// <c>SERVICE_CHANGE_REQUEST_NOT_FOUND</c>; 409 with <c>SERVICE_CHANGE_INVALID_STATE</c> when the request is already
    /// Completed or Rejected; 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectAsync(int id, [FromBody] RejectServiceChangeRequestCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { RequestId = id }, ct);
        return HandleResult(outcome);
    }
}
