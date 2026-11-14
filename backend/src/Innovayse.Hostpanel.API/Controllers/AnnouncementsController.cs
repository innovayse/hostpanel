using Innovayse.Hostpanel.Application.Support.Commands.CreateAnnouncement;
using Innovayse.Hostpanel.Application.Support.Commands.DeleteAnnouncement;
using Innovayse.Hostpanel.Application.Support.Commands.UpdateAnnouncement;
using Innovayse.Hostpanel.Application.Support.Common;
using Innovayse.Hostpanel.Application.Support.Queries.GetAnnouncement;
using Innovayse.Hostpanel.Application.Support.Queries.ListAnnouncements;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Admin endpoints for managing announcements: create, update, delete, and reads that may
/// return unpublished drafts.
/// </summary>
[Route("api/announcements")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class AnnouncementsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public AnnouncementsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns a paginated, searchable list of all announcements, drafts included.</summary>
    /// <param name="query">The page, page size, search and published state to list by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the page; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedList<AnnouncementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync([FromQuery] ListAnnouncementsQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<AnnouncementDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns a single announcement by its identifier.</summary>
    /// <param name="id">Announcement primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the announcement; 404 with <c>ANNOUNCEMENT_NOT_FOUND</c>; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AnnouncementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByIdAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<AnnouncementDto>>(new GetAnnouncementQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Creates a new announcement.</summary>
    /// <param name="command">Announcement creation details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new announcement's id; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateAnnouncementCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/announcements/{id}", id));
    }

    /// <summary>Updates an existing announcement.</summary>
    /// <param name="id">Announcement primary key; wins over any id in the body.</param>
    /// <param name="command">Updated announcement details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 with <c>ANNOUNCEMENT_NOT_FOUND</c>; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not an admin;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateAnnouncementCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { Id = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Permanently deletes an announcement.</summary>
    /// <param name="id">Announcement primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 404 with <c>ANNOUNCEMENT_NOT_FOUND</c>; 400 when the body, query or route fails validation; 401 without a session; 403 for a caller who is not an admin;
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
        var outcome = await _bus.InvokeAsync<Result>(new DeleteAnnouncementCommand(id), ct);
        return HandleResult(outcome);
    }
}
