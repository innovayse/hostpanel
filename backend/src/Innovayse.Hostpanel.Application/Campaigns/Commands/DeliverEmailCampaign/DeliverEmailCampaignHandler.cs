using System.Globalization;
using Innovayse.Hostpanel.Application.Campaigns.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Notifications.Commands.SendEmail;
using Innovayse.Hostpanel.Application.Notifications.Common;
using Innovayse.Hostpanel.Application.Notifications.Common.TemplateModels;
using Innovayse.Hostpanel.Application.Notifications.Queries.GetEmailDelivery;
using Innovayse.Hostpanel.Application.Notifications.Seeding;
using Innovayse.Hostpanel.Domain.Campaigns;
using Innovayse.Hostpanel.Domain.Campaigns.Enums;
using Innovayse.Hostpanel.Domain.Notifications.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.DeliverEmailCampaign;

/// <summary>
/// Sends the next batch of pending recipients through the email pipeline, one keyed mail each, and records how each ended on
/// the recipient. A recipient is saved after its own mail, so the counters an admin watches move as the batch goes, and a
/// crash replays only the mails whose log line says they were not delivered. When nobody is waiting any more the campaign
/// is marked sent. The campaign template is seeded at startup; a missing or switched-off one is recorded as a failed attempt
/// here, not repaired here.
/// </summary>
public sealed class DeliverEmailCampaignHandler
{
    /// <summary>The code recorded on a recipient whose mail left no delivered or refused log line: the campaign template is switched off or gone.</summary>
    private const string NotSentReason = "NOT_SENT";

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The bus the mails are sent, the clients' consent read and each outcome looked up through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The client portal's address, which the unsubscribe links point at.</summary>
    private readonly IOptions<ClientPortalOptions> _portal;

    /// <summary>The clock the delivery and finish moments are read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The bus the mails are sent, the clients' consent read and each outcome looked up through.</param>
    /// <param name="portal">The client portal's address.</param>
    /// <param name="clock">The clock the delivery and finish moments are read from.</param>
    public DeliverEmailCampaignHandler(IHostpanelDbContext context, IMessageBus bus, IOptions<ClientPortalOptions> portal, IClock clock)
    {
        _context = context;
        _bus = bus;
        _portal = portal;
        _clock = clock;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The campaign to deliver.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, also when the campaign is not sending any more (another pass finished it); or <c>CAMPAIGN_NOT_FOUND</c> when the id names no campaign.</returns>
    public async Task<Result> HandleAsync(DeliverEmailCampaignCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var campaign = await _context.EmailCampaigns.IgnoreAutoIncludes().FirstOrDefaultAsync(x => x.Id == cmd.Id, ct);
        if (campaign is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CAMPAIGN_NOT_FOUND);
        }

        if (campaign.Status != CampaignStatus.Sending)
        {
            return Result.Success();
        }

        var batch = await _context.EmailCampaignRecipients
            .Where(r => r.CampaignId == campaign.Id && r.Status == CampaignRecipientStatus.Pending)
            .OrderBy(r => r.Id)
            .Take(JobBatch.Size)
            .ToListAsync(ct);

        // The consent is read again right before the send: a client may have unsubscribed through another campaign's
        // link, or an admin may have switched marketing off, since the recipients were fixed.
        var briefs = await _bus.GetClientBriefsByIdAsync(batch.Select(r => r.ClientId).Distinct().ToList(), ct);
        foreach (var recipient in batch)
        {
            try
            {
                if (briefs.TryGetValue(recipient.ClientId, out var brief) && brief.Settings.MarketingOptIn)
                {
                    await DeliverAsync(campaign, recipient, ct);
                }
                else
                {
                    recipient.MarkUnsubscribed();
                }

                await _context.SaveChangesAsync(ct);
            }
            catch (ConcurrencyConflictException)
            {
                // A client followed the unsubscribe link while this recipient was being mailed, and its save came first. The
                // recipient already stands as unsubscribed; what is left of this batch is read again by the next pass.
                _context.DetachAll();
                return Result.Success();
            }
        }

        if (!await _context.EmailCampaignRecipients.AnyAsync(r => r.CampaignId == campaign.Id && r.Status == CampaignRecipientStatus.Pending, ct))
        {
            campaign.MarkSent(_clock.UtcNow);
            await _context.SaveChangesAsync(ct);
        }

        return Result.Success();
    }

    /// <summary>Sends one recipient its mail and records on the recipient how it ended.</summary>
    /// <param name="campaign">The campaign being delivered.</param>
    /// <param name="recipient">The pending recipient.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the outcome is staged on the recipient; the caller saves it. A new token hash is saved only before a mail that will really be sent.</returns>
    private async Task DeliverAsync(EmailCampaign campaign, EmailCampaignRecipient recipient, CancellationToken ct)
    {
        // One key per campaign and recipient: a replay after a crash finds the log line and sends nothing twice.
        var key = EmailIdempotencyKey.ForParts(
            EmailCampaignTemplateSeeder.Slug, campaign.Id.ToString(CultureInfo.InvariantCulture),
            recipient.Id.ToString(CultureInfo.InvariantCulture));
        var logKey = EmailIdempotencyKey.ForRecipient(key, recipient.Email);

        // A mail already accepted, or held by a claim still in flight, has a link in a mailbox or about to be. A new token would
        // overwrite the hash that link depends on, so the earlier log line decides the outcome and nothing is sent again.
        var earlier = (await _bus.InvokeAsync<Result<EmailDeliveryDto>>(new GetEmailDeliveryQuery(logKey), ct)).Value;
        if (earlier is { MayResend: false })
        {
            Record(recipient, earlier);
            return;
        }

        // The token exists in the mail alone; the recipient keeps its hash, and that is saved before the mail leaves so the link
        // works the moment the reader sees it.
        var link = CampaignUnsubscribeLink.For(_portal.Value.BaseUrl, recipient.IssueUnsubscribeToken());
        await _context.SaveChangesAsync(ct);

        var model = new EmailCampaignTemplateModel(campaign.Subject, campaign.Body, link);

        await _bus.InvokeAsync(
            new SendEmailCommand(
                recipient.Email, recipient.Language, EmailCampaignTemplateSeeder.Slug, model, IdempotencyKey: key, UnsubscribeUrl: link), ct);

        var delivery = await _bus.InvokeAsync<Result<EmailDeliveryDto>>(new GetEmailDeliveryQuery(logKey), ct);
        Record(recipient, delivery.Value);
    }

    /// <summary>Stages on the recipient how its mail ended, as the email log shows it.</summary>
    /// <param name="recipient">The pending recipient.</param>
    /// <param name="delivery">The log line of its mail, or <see langword="null"/> when the mail left none.</param>
    private void Record(EmailCampaignRecipient recipient, EmailDeliveryDto? delivery)
    {
        switch (delivery?.State)
        {
            case EmailDeliveryState.Sent:
                recipient.MarkSent(_clock.UtcNow);
                break;

            case EmailDeliveryState.Failed:
                recipient.RecordFailure((delivery.Failure ?? EmailSendFailure.Unknown).ToString());
                break;

            case EmailDeliveryState.Sending:
                // Another delivery holds the claim and decides the outcome; this recipient is looked at again next pass.
                break;

            default:
                recipient.RecordFailure(NotSentReason);
                break;
        }
    }
}
