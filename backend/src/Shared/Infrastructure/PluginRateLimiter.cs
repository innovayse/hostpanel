using System.Threading.RateLimiting;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Settings;

namespace Innovayse.Hostpanel.Shared.Infrastructure;

/// <summary>
/// The limiters behind the plugin routes: one sliding-window limiter per tier
/// (<see cref="RateLimitPolicies.PluginDefault"/>, <see cref="RateLimitPolicies.PluginStrict"/>,
/// <see cref="RateLimitPolicies.PluginHeavy"/>, and <see cref="RateLimitPolicies.PluginCallback"/> for the public callback
/// door) and one concurrency limiter for streams
/// (<see cref="RateLimitPolicies.PluginStream"/>), each partitioned by a key the caller builds from the plugin and the
/// caller, so one plugin or one account cannot exhaust the API.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is not a named policy.</b> A named policy is picked by an attribute before the request is routed to a
/// plugin. The tier a plugin route asks for is written in its manifest, and the account it is keyed on is resolved from
/// the signed-in user, so both are known only once the plugin call filter has matched the route. The filter therefore
/// acquires from this class, and writes a refusal through <c>RateLimitingExtensions.WriteRejectionAsync</c>, the same
/// writer the middleware uses.
/// </para>
/// <para>
/// With limiting switched off (the <c>Testing</c> environment) every partition is a no-limiter, as for the named policies.
/// </para>
/// </remarks>
public sealed class PluginRateLimiter : IDisposable
{
    /// <summary>The sliding-window limiter of each tier, by policy name.</summary>
    private readonly Dictionary<string, PartitionedRateLimiter<string>> _tiers;

    /// <summary>The concurrency limiter of streaming routes.</summary>
    private readonly PartitionedRateLimiter<string> _streams;

    /// <summary>Initializes the limiters from the validated budgets.</summary>
    /// <param name="limits">The validated rate limit options.</param>
    /// <param name="disabled">Whether limiting is switched off, so every acquisition succeeds without counting.</param>
    public PluginRateLimiter(RateLimitOptions limits, bool disabled)
    {
        ArgumentNullException.ThrowIfNull(limits);

        _tiers = new Dictionary<string, PartitionedRateLimiter<string>>(StringComparer.Ordinal)
        {
            [RateLimitPolicies.PluginDefault] = SlidingWindow(limits.PluginDefault, disabled),
            [RateLimitPolicies.PluginStrict] = SlidingWindow(limits.PluginStrict, disabled),
            [RateLimitPolicies.PluginHeavy] = SlidingWindow(limits.PluginHeavy, disabled),
            [RateLimitPolicies.PluginCallback] = SlidingWindow(limits.PluginCallback, disabled),

            // The portal's SSH key budget, taken by the plugin SSH issue path per plugin and client (#758).
            [RateLimitPolicies.SshKeys] = SlidingWindow(limits.SshKeys, disabled),
        };

        _streams = PartitionedRateLimiter.Create<string, string>(key => disabled
            ? RateLimitPartition.GetNoLimiter(key)
            : RateLimitPartition.GetConcurrencyLimiter(key, _ => RateLimiterBudgets.Concurrency(limits.PluginStreamPermitLimit)));
    }

    /// <summary>Builds the partition key of a caller of a plugin.</summary>
    /// <param name="pluginId">The plugin being called.</param>
    /// <param name="subject">Who is calling: the client account on a client call, the administrator on an admin call, each
    /// prefixed so the two can never collide.</param>
    /// <returns>The key a budget is counted under.</returns>
    public static string KeyFor(string pluginId, string subject)
    {
        return $"{pluginId}|{subject}";
    }

    /// <summary>Takes one request from a tier's budget.</summary>
    /// <param name="policy">One of <see cref="RateLimitPolicies.PluginDefault"/>, <see cref="RateLimitPolicies.PluginStrict"/>,
    /// <see cref="RateLimitPolicies.PluginHeavy"/> or <see cref="RateLimitPolicies.SshKeys"/>.</param>
    /// <param name="key">The partition, from <see cref="KeyFor"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The lease; <see cref="RateLimitLease.IsAcquired"/> is false when the budget is spent.</returns>
    /// <exception cref="ArgumentException"><paramref name="policy"/> is not a plugin tier.</exception>
    public ValueTask<RateLimitLease> AcquireAsync(string policy, string key, CancellationToken ct)
    {
        if (!_tiers.TryGetValue(policy, out var limiter))
        {
            throw new ArgumentException($"{policy} is not a plugin rate limit tier.", nameof(policy));
        }

        return limiter.AcquireAsync(key, 1, ct);
    }

    /// <summary>Takes one open stream from a caller's allowance.</summary>
    /// <param name="key">The partition, from <see cref="KeyFor"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The lease, to be disposed when the stream ends; <see cref="RateLimitLease.IsAcquired"/> is false when the
    /// caller already holds as many streams as are allowed.</returns>
    public ValueTask<RateLimitLease> AcquireStreamAsync(string key, CancellationToken ct)
    {
        return _streams.AcquireAsync(key, 1, ct);
    }

    /// <summary>Releases every limiter and the timers they keep.</summary>
    public void Dispose()
    {
        foreach (var limiter in _tiers.Values)
        {
            limiter.Dispose();
        }

        _streams.Dispose();
    }

    /// <summary>Builds the limiter of one tier.</summary>
    /// <param name="tier">The budget.</param>
    /// <param name="disabled">Whether limiting is switched off.</param>
    /// <returns>The partitioned limiter.</returns>
    private static PartitionedRateLimiter<string> SlidingWindow(RateLimitTierOptions tier, bool disabled)
    {
        return PartitionedRateLimiter.Create<string, string>(key => disabled
            ? RateLimitPartition.GetNoLimiter(key)
            : RateLimitPartition.GetSlidingWindowLimiter(key, _ => RateLimiterBudgets.SlidingWindow(tier)));
    }
}
