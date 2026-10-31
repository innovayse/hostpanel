using Innovayse.Hostpanel.API.Authorization;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Queries.GetMyEmailLog;
using Innovayse.Hostpanel.Application.Notifications.Queries.ListMyEmailLogs;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Client portal endpoints for the emails this account has been sent.
/// Every action behind the client portal policy may also answer 503 with <c>IDENTITY_PLUGIN_UNAVAILABLE</c> (and <c>Retry-After</c>)
/// when the enabled identity plugin cannot say which account the caller acts for; the convention
/// <c>IdentityPluginUnavailableConvention</c> declares it once in the generated document.
/// </summary>
/// <remarks>
/// The same read as the admin route on <see cref="EmailLogsController"/>, with the client id
/// resolved from the credential rather than taken from the path: a person may read their own
/// correspondence and nobody else's, and an id in the URL is exactly how that goes wrong. Each
/// action dispatches once; the handler behind it resolves the caller and scopes the read.
/// </remarks>
[Route("api/me/emails")]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class MyEmailsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public MyEmailsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Returns the emails sent to the authenticated client, newest first.</summary>
    /// <param name="query">The page and page size; the read clamps the size to 1-100.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the page; 401 without a session; 404 with <c>CLIENT_PROFILE_NOT_FOUND</c>;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewEmails</c>;
    /// 400 when the body, query or route fails validation; 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewEmails)]
    [ProducesResponseType(typeof(PaginatedList<MyEmailLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyEmailsAsync(
        [FromQuery] ListMyEmailLogsQuery query,
        CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<PaginatedList<MyEmailLogDto>>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Returns one email the authenticated client was sent, body included.</summary>
    /// <param name="id">The log entry's primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the email; 404 with <c>EMAIL_LOG_NOT_FOUND</c> when it does not exist or belongs
    /// to another account: the two are answered alike on purpose, see
    /// <see cref="Application.Notifications.Queries.GetClientEmailLog.GetClientEmailLogHandler"/>;
    /// 401 without a session; 404 with <c>CLIENT_PROFILE_NOT_FOUND</c>;
    /// 403 with <c>CLIENT_PERMISSION_DENIED</c> when the user lacks <c>ViewEmails</c>;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}")]
    [Authorize(Policy = Policies.Client)]
    [RequiresClientPermission(ClientPermission.ViewEmails)]
    [ProducesResponseType(typeof(MyEmailLogDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyEmailAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<MyEmailLogDetailDto>>(new GetMyEmailLogQuery(id), ct);
        return HandleResult(outcome);
    }
}
