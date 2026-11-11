using Innovayse.Hostpanel.Application.Campaigns.Commands.UnsubscribeFromEmailCampaigns;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// The unsubscribe link of a campaign mail. Anonymous by design: the reader follows the link from a mail with no session, and
/// the personal token in it is the proof. A controller of its own rather than an action on the admin campaigns controller,
/// for the reason <see cref="ContactController"/> stands apart: a world-reachable action must not hang off a class whose
/// <c>[Authorize]</c> says otherwise.
/// </summary>
/// <remarks>
/// The page endpoint is rate limited with <see cref="RateLimitPolicies.Strict"/>: the token is 256 random bits, so guessing one
/// is out of reach, and the budget only keeps the endpoint from being used as a free write path. The one-click action is held
/// to the global limit only, like the tracking routes: mail providers post the one-click requests of all their users from a few
/// shared addresses, so a small per-address budget would drop real unsubscribes.
/// </remarks>
[Route("api/public/unsubscribe")]
[AllowAnonymous]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class PublicUnsubscribeController : BaseApiController
{
    /// <summary>Wolverine bus the action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus the action dispatches through.</param>
    public PublicUnsubscribeController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Stops marketing email to the client the link's token belongs to. Following the same link again changes nothing.</summary>
    /// <param name="command">The token from the unsubscribe link.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 once the client no longer receives campaign mail; 400 with <c>VALIDATION_ERROR</c> when the token is missing or oversized;
    /// 404 with <c>CAMPAIGN_UNSUBSCRIBE_LINK_NOT_FOUND</c> when the token names nobody;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Strict)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnsubscribeAsync([FromBody] UnsubscribeFromEmailCampaignsCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command, ct);
        return HandleResult(outcome);
    }

    /// <summary>
    /// The address in the <c>List-Unsubscribe</c> header of a campaign mail (RFC 8058): the mail client posts to it, with the
    /// body <c>List-Unsubscribe=One-Click</c>, when the reader presses its own unsubscribe button. The token rides in the query
    /// because the address is fixed in the mail; the body carries nothing the outcome depends on, so it is not read. No session
    /// cookie is involved, so there is nothing for a cross-site page to ride on and no custom header is asked for.
    /// </summary>
    /// <param name="command">The token from the address.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The same outcomes as <see cref="UnsubscribeAsync"/>, with the global rate limit instead of the strict one.</returns>
    [HttpPost("one-click")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnsubscribeOneClickAsync([FromQuery] UnsubscribeFromEmailCampaignsCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command, ct);
        return HandleResult(outcome);
    }
}
