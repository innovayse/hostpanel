namespace Innovayse.API.Support;

using Innovayse.API.Support.Requests;
using Innovayse.Application.Common;
using Innovayse.Application.Support.Commands.CreateAnnouncement;
using Innovayse.Application.Support.Commands.DeleteAnnouncement;
using Innovayse.Application.Support.Commands.UpdateAnnouncement;
using Innovayse.Application.Support.DTOs;
using Innovayse.Application.Support.Queries.GetAnnouncement;
using Innovayse.Application.Support.Queries.ListAnnouncements;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for managing announcements.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/announcements")]
[Authorize(Roles = Roles.Admin)]
public sealed class AnnouncementsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns a paginated list of all announcements.</summary>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page (default 20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated announcement list.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResult<AnnouncementDto>>> GetAllAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<PagedResult<AnnouncementDto>>(
            new ListAnnouncementsQuery(page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Returns a single announcement by its identifier.</summary>
    /// <param name="id">Announcement primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Full announcement DTO.</returns>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<AnnouncementDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var dto = await bus.InvokeAsync<AnnouncementDto>(new GetAnnouncementQuery(id), ct);
        return Ok(dto);
    }

    /// <summary>Creates a new announcement.</summary>
    /// <param name="request">Announcement creation details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created announcement identifier.</returns>
    [HttpPost]
    public async Task<ActionResult<int>> CreateAsync([FromBody] CreateAnnouncementRequest request, CancellationToken ct)
    {
        var id = await bus.InvokeAsync<int>(
            new CreateAnnouncementCommand(request.Title, request.Content, request.IsPublished), ct);
        return StatusCode(StatusCodes.Status201Created, id);
    }

    /// <summary>Updates an existing announcement.</summary>
    /// <param name="id">Announcement primary key.</param>
    /// <param name="request">Updated announcement details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateAnnouncementRequest request, CancellationToken ct)
    {
        await bus.InvokeAsync(
            new UpdateAnnouncementCommand(id, request.Title, request.Content, request.IsPublished), ct);
        return NoContent();
    }

    /// <summary>Permanently deletes an announcement.</summary>
    /// <param name="id">Announcement primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        await bus.InvokeAsync(new DeleteAnnouncementCommand(id), ct);
        return NoContent();
    }
}
