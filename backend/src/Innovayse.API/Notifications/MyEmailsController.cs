namespace Innovayse.API.Notifications;

using Innovayse.Application.Clients.DTOs;
using Innovayse.Application.Clients.Queries.GetMyProfile;
using Innovayse.Application.Common;
using Innovayse.Application.Notifications.DTOs;
using Innovayse.Application.Notifications.Queries.GetClientEmailLog;
using Innovayse.Application.Notifications.Queries.ListClientEmailLogs;
using Innovayse.Domain.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

/// <summary>
/// Client portal endpoints for the emails this account has been sent.
/// </summary>
/// <param name="bus">Wolverine message bus.</param>
/// <remarks>
/// The same read as the admin route on <see cref="EmailLogsController"/>, with the client id
/// resolved from the credential rather than taken from the path: a person may read their own
/// correspondence and nobody else's, and an id in the URL is exactly how that goes wrong.
/// </remarks>
[ApiController]
[Route("api/me/emails")]
[Authorize(Roles = Roles.Client)]
public sealed class MyEmailsController(IMessageBus bus) : ControllerBase
{
    /// <summary>
    /// Returns the emails sent to the authenticated client, newest first.
    /// </summary>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Entries per page; the query clamps this to 1–100.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A page of email log entries.</returns>
    [HttpGet]
    public async Task<ActionResult<PagedResult<EmailLogDto>>> GetMyEmailsAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var profile = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(), ct);

        var result = await bus.InvokeAsync<PagedResult<EmailLogDto>>(
            new ListClientEmailLogsQuery(profile.Id, page, pageSize), ct);

        return Ok(result);
    }

    /// <summary>
    /// Returns one email the authenticated client was sent, body included.
    /// </summary>
    /// <param name="id">The log entry's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The email, or 404 when it does not exist or belongs to another account.</returns>
    /// <remarks>
    /// Both cases answer 404 on purpose: a distinct "not yours" would confirm that an entry
    /// exists to whoever guessed the number.
    /// </remarks>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<EmailLogDetailDto>> GetMyEmailAsync(int id, CancellationToken ct)
    {
        var profile = await bus.InvokeAsync<ClientDto>(new GetMyProfileQuery(), ct);

        var email = await bus.InvokeAsync<EmailLogDetailDto?>(
            new GetClientEmailLogQuery(profile.Id, id), ct);

        return email is null ? NotFound() : Ok(email);
    }
}
