using Innovayse.Hostpanel.Application.Campaigns.Commands.CancelEmailCampaignSchedule;
using Innovayse.Hostpanel.Application.Campaigns.Commands.CreateEmailCampaign;
using Innovayse.Hostpanel.Application.Campaigns.Commands.DeleteEmailCampaign;
using Innovayse.Hostpanel.Application.Campaigns.Commands.ScheduleEmailCampaign;
using Innovayse.Hostpanel.Application.Campaigns.Commands.UpdateEmailCampaign;
using Innovayse.Hostpanel.Application.Campaigns.Common;
using Innovayse.Hostpanel.Application.Campaigns.Queries.CountEmailCampaignAudience;
using Innovayse.Hostpanel.Application.Campaigns.Queries.GetEmailCampaign;
using Innovayse.Hostpanel.Application.Campaigns.Queries.ListEmailCampaigns;
using Innovayse.Hostpanel.Application.Campaigns.Queries.PreviewEmailCampaign;
using Innovayse.Hostpanel.Shared.Authorization;
using Innovayse.Hostpanel.Shared.Controllers;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace Innovayse.Hostpanel.API.Controllers;

/// <summary>
/// Admin endpoints for email campaigns: write a campaign to the clients who agreed to marketing email, choose who it
/// reaches, queue it for sending or send it now, and take it back to a draft.
/// </summary>
[Route("api/admin/email-campaigns")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status429TooManyRequests)]
public sealed class EmailCampaignsController : BaseApiController
{
    /// <summary>Wolverine bus every action dispatches through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Creates the controller.</summary>
    /// <param name="bus">Wolverine bus every action dispatches through.</param>
    public EmailCampaignsController(IMessageBus bus)
    {
        _bus = bus;
    }

    /// <summary>Every email campaign, newest first, with how its delivery stands.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the campaigns; 400 with <c>VALIDATION_ERROR</c>; 401 without a session; 403 for a caller who is not an administrator;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmailCampaignListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<IReadOnlyList<EmailCampaignListItemDto>>>(new ListEmailCampaignsQuery(), ct);
        return HandleResult(outcome);
    }

    /// <summary>A single email campaign, with its body, segments and delivery stats.</summary>
    /// <param name="id">The campaign primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the campaign; 400 with <c>VALIDATION_ERROR</c> for a non-positive id; 401 without a session; 403 for a caller who is not
    /// an administrator; 404 with <c>CAMPAIGN_NOT_FOUND</c>;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EmailCampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<EmailCampaignDto>>(new GetEmailCampaignQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>A saved campaign rendered the way its recipients would see it, with a sample unsubscribe link. Nothing is sent.</summary>
    /// <param name="id">The campaign primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the rendered subject and HTML; 400 with <c>VALIDATION_ERROR</c> for a non-positive id; 401 without a session; 403 for a
    /// caller who is not an administrator; 404 with <c>CAMPAIGN_NOT_FOUND</c>, or with <c>EMAIL_TEMPLATE_NOT_FOUND</c> when the campaign
    /// mail template was deleted;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpGet("{id:int}/preview")]
    [ProducesResponseType(typeof(EmailCampaignPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PreviewAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<EmailCampaignPreviewDto>>(new PreviewEmailCampaignQuery(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>How many clients a set of segments reaches, counted with the rules sending uses. Nothing is saved.</summary>
    /// <param name="query">The segments to count.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 200 with the count; 400 with <c>VALIDATION_ERROR</c> when a segment is refused; 401 without a session; 403 for a caller who is not
    /// an administrator;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("audience-count")]
    [ProducesResponseType(typeof(EmailCampaignAudienceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CountAudienceAsync([FromBody] CountEmailCampaignAudienceQuery query, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<EmailCampaignAudienceDto>>(query, ct);
        return HandleResult(outcome);
    }

    /// <summary>Writes a new campaign as a draft; the calling admin is recorded as its author.</summary>
    /// <param name="command">The subject, body and segments.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 201 with the new campaign's id; 400 with <c>VALIDATION_ERROR</c> when a field is refused; 401 without a session; 403 for a caller
    /// who is not an administrator;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAsync([FromBody] CreateEmailCampaignCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result<int>>(command, ct);
        return HandleResult(outcome, id => CreatedResult($"/api/admin/email-campaigns/{id}", id));
    }

    /// <summary>Replaces the subject, body and segments of a draft campaign.</summary>
    /// <param name="id">The campaign primary key; wins over any id in the body.</param>
    /// <param name="command">The new subject, body and segments.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 with <c>VALIDATION_ERROR</c> when a field is refused; 401 without a session; 403 for a caller who is not an
    /// administrator; 404 with <c>CAMPAIGN_NOT_FOUND</c>; 409 with <c>CAMPAIGN_NOT_DRAFT</c> when the campaign has been scheduled or sent;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateEmailCampaignCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { Id = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Deletes a draft campaign.</summary>
    /// <param name="id">The campaign primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 with <c>VALIDATION_ERROR</c> for a non-positive id; 401 without a session; 403 for a caller who is not an
    /// administrator; 404 with <c>CAMPAIGN_NOT_FOUND</c>; 409 with <c>CAMPAIGN_NOT_DRAFT</c> when the campaign has been scheduled or sent;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new DeleteEmailCampaignCommand(id), ct);
        return HandleResult(outcome);
    }

    /// <summary>Queues a draft campaign for sending at a future moment, or sends it now when no moment is given.</summary>
    /// <param name="id">The campaign primary key; wins over any id in the body.</param>
    /// <param name="command">The moment to send at, or none to send now.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 with <c>VALIDATION_ERROR</c> when a field is refused, <c>CAMPAIGN_NO_SEGMENT</c> when the campaign reaches
    /// nobody, or <c>CAMPAIGN_SCHEDULE_IN_PAST</c> when the moment is not in the future; 401 without a session; 403 for a caller who is not
    /// an administrator; 404 with <c>CAMPAIGN_NOT_FOUND</c>; 409 with <c>CAMPAIGN_NOT_DRAFT</c> when the campaign has already been scheduled or sent;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/schedule")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ScheduleAsync(int id, [FromBody] ScheduleEmailCampaignCommand command, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(command with { Id = id }, ct);
        return HandleResult(outcome);
    }

    /// <summary>Takes a scheduled campaign back to a draft so it can be edited again.</summary>
    /// <param name="id">The campaign primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// 204 on success; 400 with <c>VALIDATION_ERROR</c> for a non-positive id; 401 without a session; 403 for a caller who is not an
    /// administrator; 404 with <c>CAMPAIGN_NOT_FOUND</c>; 409 with <c>CAMPAIGN_NOT_SCHEDULED</c> when the campaign is not waiting to be sent;
    /// 429 when the rate limit is hit.
    /// </returns>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelAsync(int id, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new CancelEmailCampaignScheduleCommand(id), ct);
        return HandleResult(outcome);
    }
}
