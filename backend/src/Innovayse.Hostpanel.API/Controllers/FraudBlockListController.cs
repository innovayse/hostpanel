using Innovayse.Hostpanel.Application.Fraud.Commands.AddBlockListEntry;
using Innovayse.Hostpanel.Application.Fraud.Commands.DeleteBlockListEntry;
using Innovayse.Hostpanel.Application.Fraud.Common;
using Innovayse.Hostpanel.Application.Fraud.Queries.ListBlockListEntries;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>Admin endpoints for the fraud block list: IP addresses, IP ranges, e-mail domains and countries that are refused.</summary>
[Route("api/admin/fraud/blocklist")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class FraudBlockListController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public FraudBlockListController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns a page of the block list, newest first, optionally for one type.</summary>
    /// <param name="query">The page, page size and type filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the page; 400 with <c>VALIDATION_ERROR</c> for a bad paging window or an unknown type; 401 without a session; 403 for a
    /// caller who is not an administrator;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<BlockListEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync([FromQuery] ListBlockListEntriesQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<BlockListEntryDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Adds an IP address, IP range, e-mail domain or country to the block list.</summary>
    /// <param name="command">The type, the value and an optional note.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new entry's id; 400 with <c>VALIDATION_ERROR</c> when the type, the value for that type or the note is refused;
    /// 401 without a session; 403 for a caller who is not an administrator; 409 with <c>FRAUD_BLOCKLIST_ENTRY_EXISTS</c> when the value is
    /// listed already for that type;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddAsync([FromBody] AddBlockListEntryCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/admin/fraud/blocklist/{id}", id));
    }

    /// <summary>Removes an entry from the block list.</summary>
    /// <param name="id">The entry's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 when removed; 400 with <c>VALIDATION_ERROR</c> for a non-positive id; 401 without a session; 403 for a caller who is not an
    /// administrator; 404 with <c>FRAUD_BLOCKLIST_ENTRY_NOT_FOUND</c> when the id names no entry;
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
        var outcome = await _bus.InvokeAsync<Result>(new DeleteBlockListEntryCommand(id), ct);
        return HandleResult(outcome);
    }
}
