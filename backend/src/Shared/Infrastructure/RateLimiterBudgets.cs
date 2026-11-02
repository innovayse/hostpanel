using System.Threading.RateLimiting;
using Innovayse.Hostpanel.Shared.Settings;

namespace Innovayse.Hostpanel.Shared.Infrastructure;

/// <summary>
/// Turns a configured budget into the options of the limiter that enforces it. The one place that decides how every
/// limiter of this API behaves when it is full, so the named policies and the plugin limiter cannot drift apart.
/// </summary>
public static class RateLimiterBudgets
{
    /// <summary>Builds the options of a sliding-window limiter for one tier.</summary>
    /// <param name="tier">The budget to apply.</param>
    /// <returns>The limiter options.</returns>
    public static SlidingWindowRateLimiterOptions SlidingWindow(RateLimitTierOptions tier)
    {
        ArgumentNullException.ThrowIfNull(tier);

        return new SlidingWindowRateLimiterOptions
        {
            PermitLimit = tier.PermitLimit,
            Window = TimeSpan.FromSeconds(tier.WindowSeconds),
            SegmentsPerWindow = RateLimitOptions.SegmentsPerWindow,

            // Refused outright rather than held. A queue makes a rate limit into a latency
            // limit: the caller still gets served, just later, so an abusive client keeps its
            // throughput and merely occupies connections while doing it.
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        };
    }

    /// <summary>Builds the options of a concurrency limiter.</summary>
    /// <param name="permitLimit">How many operations may be in flight at once.</param>
    /// <returns>The limiter options.</returns>
    public static ConcurrencyLimiterOptions Concurrency(int permitLimit)
    {
        return new ConcurrencyLimiterOptions
        {
            PermitLimit = permitLimit,

            // Nothing queues. A queued export holds its connection open while the
            // ones ahead of it hold table scans in memory, so waiting turns one
            // caller's burst into a slow request for everybody; refusing tells
            // them to come back instead.
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        };
    }
}
