namespace Innovayse.API.Support;

using Innovayse.Application.Common;
using Innovayse.Application.Support.Queries.ListPublishedAnnouncements;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// The announcements feed the client portal reads.
/// </summary>
/// <remarks>
/// A separate controller rather than a relaxed <c>[Authorize]</c> on <c>AnnouncementsController</c>:
/// that class is admin surface end to end -- create, update, delete, and a read that returns
/// drafts -- and authorization there is declared once for the whole class. Loosening it, even for
/// the one GET, would put a client-reachable action on the same type as the writes and leave the
/// next person adding an action there to notice that the class attribute no longer means what it
/// says. The knowledge base already splits this way (<c>KnowledgebaseController</c> beside
/// <c>KnowledgebaseAdminController</c>), and this follows it.
/// <para>
/// Authenticated rather than anonymous. This is the portal's "Recent News" card, and published
/// does not have to mean world-readable; opening the feed to the internet is a content decision
/// nobody has taken, whereas letting a signed-in customer read it is exactly the defect.
/// </para>
/// </remarks>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/announcements/published")]
[Authorize]
public sealed class PublishedAnnouncementsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns a paginated list of published announcements, newest first.</summary>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page (default 20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated published announcements, without the editorial published flag.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResult<PublishedAnnouncementDto>>> GetPublishedAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<PagedResult<PublishedAnnouncementDto>>(
            new ListPublishedAnnouncementsQuery(page, pageSize), ct);
        return Ok(result);
    }
}
