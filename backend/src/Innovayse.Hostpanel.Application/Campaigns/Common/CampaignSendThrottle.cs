using System.Globalization;
using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Campaigns.Common;

/// <summary>
/// Tells the campaign sender how many mails it may still send right now: the operator's
/// <see cref="CampaignSettingKeys.SendRatePerMinute"/> less the mails already delivered in the last minute, over every
/// campaign. The window is read from the recipients' own delivery moments, so the limit holds however often the dispatch
/// job runs, across all campaigns and instances, and needs no state of its own: a pass that finds the minute used up sends
/// nothing and the mails wait for a later one.
/// </summary>
/// <remarks>
/// The count and the sends that follow it are not atomic: the throttle relies on the dispatch cron's lease, which lets one
/// dispatcher run at a time, so no second pass can read the same free capacity and send past the limit. Run from anywhere
/// that is not held by that lease, it can overshoot. The rate is read through <see cref="SettingSource"/>, which falls back to the seeded default with a warning when the row is
/// missing or invalid: a typo in System Settings must not lift the limit. An attempt that failed has no delivery moment and is not counted; a recipient gets at
/// most <c>EmailCampaignRecipient.MaxAttempts</c> of them.
/// </remarks>
public sealed class CampaignSendThrottle
{
    /// <summary>The length of the window the rate is measured over.</summary>
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the stored rate and falls back to the default.</summary>
    private readonly SettingSource _source;

    /// <summary>The clock the window is measured from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the throttle.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="source">Reads the stored rate and falls back to the default.</param>
    /// <param name="clock">The clock the window is measured from.</param>
    public CampaignSendThrottle(IHostpanelDbContext context, SettingSource source, IClock clock)
    {
        _context = context;
        _source = source;
        _clock = clock;
    }

    /// <summary>Counts how many mails may still be sent in the current minute.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The mails the sender may send now: the rate less what the last minute already carried, never below zero and never above
    /// one job batch, which is the most any pass reads at a time.
    /// </returns>
    public async Task<int> RemainingAsync(CancellationToken ct)
    {
        var rows = await _source.ReadRowsAsync([CampaignSettingKeys.SendRatePerMinute], ct);
        var rate = int.Parse(_source.Value(rows, CampaignSettingKeys.SendRatePerMinute), CultureInfo.InvariantCulture);
        var windowStart = _clock.UtcNow - Window;
        var sent = await _context.EmailCampaignRecipients.AsNoTracking().CountAsync(r => r.SentAt > windowStart, ct);

        return Math.Clamp(rate - sent, 0, JobBatch.Size);
    }
}
