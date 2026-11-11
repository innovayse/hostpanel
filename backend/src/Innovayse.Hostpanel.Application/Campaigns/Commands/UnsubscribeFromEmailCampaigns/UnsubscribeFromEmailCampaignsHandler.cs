using Innovayse.Hostpanel.Application.Clients.Commands.WithdrawClientMarketingConsent;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Campaigns;
using Innovayse.Hostpanel.Domain.Campaigns.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.UnsubscribeFromEmailCampaigns;

/// <summary>
/// Withdraws the marketing consent of the client behind the token and records the unsubscribe on that recipient. The consent
/// is kept on the client, so every later campaign skips the client, not just this one. Following the link again is harmless.
/// The recipient is found by the hash of the token, which is all that is stored of it.
/// </summary>
public sealed class UnsubscribeFromEmailCampaignsHandler
{
    /// <summary>How many times the recipient is read and marked again when the sender saves it first.</summary>
    private const int MaxAttempts = 3;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The bus the client's consent is withdrawn through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The bus the client's consent is withdrawn through.</param>
    public UnsubscribeFromEmailCampaignsHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The token from the unsubscribe link.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, also when the link was followed before or the sender saved the recipient at the same moment; <c>CAMPAIGN_UNSUBSCRIBE_LINK_NOT_FOUND</c> when the token names no recipient.</returns>
    public async Task<Result> HandleAsync(UnsubscribeFromEmailCampaignsCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var tokenHash = EmailCampaignRecipient.HashToken(cmd.Token);
        var clientId = await _context.EmailCampaignRecipients.AsNoTracking()
            .Where(r => r.UnsubscribeTokenHash == tokenHash)
            .Select(r => (int?)r.ClientId)
            .FirstOrDefaultAsync(ct);
        if (clientId is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.CAMPAIGN_UNSUBSCRIBE_LINK_NOT_FOUND);
        }

        // The consent is what the reader asked for and what stops the mail; it is withdrawn before anything about the
        // campaign's own bookkeeping, and nothing below can undo it.
        var withdrawn = await _bus.InvokeAsync<Result>(new WithdrawClientMarketingConsentCommand(clientId.Value), ct);
        if (withdrawn.IsFailure)
        {
            return withdrawn;
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var recipient = await _context.EmailCampaignRecipients.FirstOrDefaultAsync(r => r.UnsubscribeTokenHash == tokenHash, ct);

            // A mail that failed was never read, and one already unsubscribed from stays as it is; only the others change.
            if (recipient is null || recipient.Status is not (CampaignRecipientStatus.Pending or CampaignRecipientStatus.Sent))
            {
                break;
            }

            recipient.MarkUnsubscribed();
            try
            {
                await _context.SaveChangesAsync(ct);
                break;
            }
            catch (ConcurrencyConflictException)
            {
                // The sender saved this recipient (sent, or skipped it) between the read and this save. Read it again and
                // mark what is there now. When every attempt loses, the consent is already withdrawn, which is what the
                // reader asked for and all the next pass needs to skip the recipient.
                _context.DetachAll();
            }
        }

        return Result.Success();
    }
}
