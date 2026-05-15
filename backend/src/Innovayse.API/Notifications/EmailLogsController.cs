namespace Innovayse.API.Notifications;

using Innovayse.Application.Notifications.DTOs;
using Innovayse.Application.Notifications.Queries.ListEmailLogs;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Admin endpoints for viewing the email delivery log.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
[ApiController]
[Route("api/admin/email-logs")]
[Authorize(Roles = Roles.Admin)]
public sealed class EmailLogsController(IMessageBus bus) : ControllerBase
{
    /// <summary>Returns a paginated list of email log entries, newest first.</summary>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Number of entries per page (default 20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated email log entries.</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmailLogDto>>> GetAllAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await bus.InvokeAsync<IReadOnlyList<EmailLogDto>>(
            new ListEmailLogsQuery(page, pageSize), ct);
        return Ok(result);
    }
}
