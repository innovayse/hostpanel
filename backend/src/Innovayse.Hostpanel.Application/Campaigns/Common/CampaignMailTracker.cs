using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Campaigns;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Campaigns.Common;

/// <summary>
/// Writes an open or a click onto the recipient a tracking token names. The recipient row is also written by the sender and by the
/// unsubscribe page, and its version check refuses the second of two writers, so a save that loses reads the row again and
/// repeats; a tracking hit is never worth a failure the reader would see.
/// </summary>
public static class CampaignMailTracker
{
    /// <summary>How many times the recipient is read and written again when another writer saves it first.</summary>
    private const int MaxAttempts = 3;

    /// <summary>Applies a change to the recipient the token names and saves it.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="token">The recipient's tracking token, as it appears in the link.</param>
    /// <param name="record">The domain method to apply, such as recording an open.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the token names a recipient, also when every save lost to another writer (the hit is then not counted); <see langword="false"/> when it names nobody.</returns>
    public static async Task<bool> RecordAsync(
        IHostpanelDbContext context, string token, Action<EmailCampaignRecipient> record, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(record);

        var tokenHash = EmailCampaignRecipient.HashToken(token);
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var recipient = await context.EmailCampaignRecipients.FirstOrDefaultAsync(r => r.TrackingTokenHash == tokenHash, ct);
            if (recipient is null)
            {
                return false;
            }

            record(recipient);
            try
            {
                await context.SaveChangesAsync(ct);
                return true;
            }
            catch (ConcurrencyConflictException)
            {
                // The sender or the unsubscribe page saved this recipient between the read and this save; the row is read again.
                context.DetachAll();
            }
        }

        return true;
    }
}
