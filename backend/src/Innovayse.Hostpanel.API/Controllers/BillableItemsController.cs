using Innovayse.Hostpanel.Application.Billing.Commands.CreateBillableItem;
using Innovayse.Hostpanel.Application.Billing.Commands.DeleteBillableItem;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Queries.ListBillableItems;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>Admin endpoints for managing billable items.</summary>
[Route("api/billing/billable-items")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class BillableItemsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public BillableItemsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns a paginated list of billable items, narrowed by a search, a client, a type and an invoiced filter.</summary>
    /// <param name="query">The page, page size, search text and filters to list by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the page; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<BillableItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync([FromQuery] ListBillableItemsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<BillableItemDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Creates a new billable item.</summary>
    /// <param name="command">The billable item to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new billable item ID; 400 when the body, query or route fails validation; 404 when the client does not exist; 401 without a session; 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateBillableItemCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult("/api/billing/billable-items", id));
    }

    /// <summary>Deletes a billable item.</summary>
    /// <param name="id">Billable item primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when deleted; 404 with <c>BILLABLE_ITEM_NOT_FOUND</c>; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new DeleteBillableItemCommand(id), ct);
        return HandleResult(outcome);
    }
}
