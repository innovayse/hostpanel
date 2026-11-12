using Innovayse.Hostpanel.Application.Campaigns.Commands.RecordEmailCampaignClick;
using Innovayse.Hostpanel.Application.Campaigns.Commands.RecordEmailCampaignOpen;
using Innovayse.Hostpanel.Application.Campaigns.Common;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// The open pixel and the tracked links of a campaign mail. Anonymous by design: a mail client loads the image and a reader
/// follows the link with no session, and the personal token in the address is the proof. A controller of its own for the reason
/// <see cref="PublicUnsubscribeController"/> stands apart: a world-reachable action must not hang off a class whose
/// <c>[Authorize]</c> says otherwise.
/// </summary>
/// <remarks>
/// Held to the global rate limit only, not to the strict tier of the unsubscribe link: the image proxies of large mail providers fetch every
/// pixel from a few addresses, so a small per-address budget would drop real opens. A click never carries its own destination:
/// the address comes from the campaign's stored body, so this controller cannot be used as an open redirect.
/// </remarks>
[Route(CampaignTrackingLinks.RoutePrefix)]
[AllowAnonymous]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class PublicCampaignTrackingController : BaseApiController
{
    /// <summary>A transparent 1x1 GIF, the body of every pixel answer.</summary>
    private static readonly byte[] Pixel = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");

    /// <summary>Wolverine bus the actions dispatch through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus the actions dispatch through.</param>
    public PublicCampaignTrackingController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Counts an open of the mail the token belongs to and answers the transparent pixel. No cache may keep the answer, or later opens would not reach the server.</summary>
    /// <param name="token">The recipient's mail token from the pixel address.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with a one-pixel GIF and <c>Cache-Control: no-store</c>; 400 with <c>VALIDATION_ERROR</c> when the token is oversized;
    /// 404 with <c>CAMPAIGN_MAIL_LINK_NOT_FOUND</c> when the token names nobody;
    /// 429 when the global rate limit is hit.
    /// </returns>
    [HttpGet(CampaignTrackingLinks.OpenRoute)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "image/gif")]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> OpenAsync(string token, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new RecordEmailCampaignOpenCommand(token), ct);
        return HandleUncachedImage(outcome, Pixel, "image/gif");
    }

    /// <summary>Counts a click on the link at an index of the mail the token belongs to and sends the reader to it.</summary>
    /// <param name="token">The recipient's mail token from the link.</param>
    /// <param name="index">The position of the link among the trackable links of the campaign body.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 302 to the absolute http or https address the campaign body holds at that index, with <c>Cache-Control: no-store</c>;
    /// 400 with <c>VALIDATION_ERROR</c> when the token is oversized or the index negative;
    /// 404 with <c>CAMPAIGN_MAIL_LINK_NOT_FOUND</c> when the token names nobody or the body has no link at that index;
    /// 429 when the global rate limit is hit.
    /// </returns>
    [HttpGet(CampaignTrackingLinks.ClickRoute)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClickAsync(string token, int index, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<string>>(new RecordEmailCampaignClickCommand(token, index), ct);
        return HandleRedirect(outcome);
    }
}
