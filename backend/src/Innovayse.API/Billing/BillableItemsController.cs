namespace Innovayse.API.Billing;

using Innovayse.API.Billing.Requests;
using Innovayse.Application.Billing.Commands.CreateBillableItem;
using Innovayse.Application.Billing.Commands.DeleteBillableItem;
using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Billing.Queries.ListBillableItems;
using Innovayse.Application.Common;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing billable items.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/billing/billable-items")]
[Authorize(Roles = Roles.Admin)]
public sealed class BillableItemsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns a paginated list of billable items with optional type filter.</summary>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page (default 20, max 100).</param>
    /// <param name="type">Optional type filter (OneTime or Recurring).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated billable item list.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResult<BillableItemDto>>> GetAllAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? type = null,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<PagedResult<BillableItemDto>>(
            new ListBillableItemsQuery(page, pageSize, type), ct);
        return Ok(result);
    }

    /// <summary>Creates a new billable item.</summary>
    /// <param name="request">Billable item creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>201 Created with the new billable item ID.</returns>
    [HttpPost]
    public async Task<ActionResult<int>> CreateAsync(
        [FromBody] CreateBillableItemRequest request,
        CancellationToken ct)
    {
        var cmd = new CreateBillableItemCommand(
            request.ClientId,
            request.Description,
            request.Amount,
            request.Currency,
            request.Type,
            request.RecurringPeriod,
            request.NextDueDate);

        var id = await bus.InvokeAsync<int>(cmd, ct);
        return StatusCode(StatusCodes.Status201Created, id);
    }

    /// <summary>Deletes a billable item.</summary>
    /// <param name="id">Billable item primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteBillableItemCommand(id), ct);
        return NoContent();
    }
}
