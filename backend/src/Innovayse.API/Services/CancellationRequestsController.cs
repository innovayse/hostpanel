namespace Innovayse.API.Services;

using Innovayse.Application.Common;
using Innovayse.Application.Services.Commands.DeleteCancellationRequest;
using Innovayse.Application.Services.DTOs;
using Innovayse.Application.Services.Queries.ListCancellationRequests;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing cancellation requests.
/// Requires Admin role.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/admin/cancellation-requests")]
[Authorize(Roles = Roles.Admin)]
public sealed class CancellationRequestsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns a paginated list of all cancellation requests.</summary>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Items per page (max 100).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paged list of cancellation request DTOs.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResult<CancellationRequestDto>>> GetAllAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<PagedResult<CancellationRequestDto>>(
            new ListCancellationRequestsQuery(page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Deletes a cancellation request by primary key.</summary>
    /// <param name="id">Cancellation request primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content.</returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteCancellationRequestCommand(id), ct);
        return NoContent();
    }
}
