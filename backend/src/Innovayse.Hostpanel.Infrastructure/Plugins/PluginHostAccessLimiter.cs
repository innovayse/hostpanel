using System.Globalization;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Shared.Infrastructure;

/*
 * The plugin host access path is reached from inside a plugin call, after the route's own plugin tier was taken, so no
 * named policy can sit in front of it. It takes from the same in-process PluginRateLimiter the plugin routes use, under
 * the budget the key names (the portal's ssh-keys for SSH), partitioned per plugin and client account ("client:" prefix, as the call filter keys a
 * client call). The lease is a sliding-window permit, released by the window, so it is disposed at once.
 *
 * The budget is per process, like every PluginRateLimiter tier: with several API instances each keeps its own window,
 * so a plugin can issue up to the budget times the number of instances. It is a flood guard, not the authority on how
 * many keys exist; the per-service key limit, checked under the service row's concurrency token, is that authority and
 * holds across instances. The supported deployment is one API instance.
 */

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary><see cref="IPluginHostAccessLimiter"/> over the host's <see cref="PluginRateLimiter"/>.</summary>
public sealed class PluginHostAccessLimiter : IPluginHostAccessLimiter
{
    /// <summary>The prefix of a client subject in a plugin partition key, as the plugin call filter writes it.</summary>
    private const string ClientSubjectPrefix = "client:";

    /// <summary>The host's plugin limiters, whose tiers this takes from.</summary>
    private readonly PluginRateLimiter _limiter;

    /// <summary>Initializes the adapter.</summary>
    /// <param name="limiter">The host's plugin limiters, whose tiers this takes from.</param>
    public PluginHostAccessLimiter(PluginRateLimiter limiter)
    {
        _limiter = limiter;
    }

    /// <inheritdoc/>
    public async Task<bool> TryAcquireAsync(PluginHostAccessKey key, CancellationToken ct)
    {
        var partition = PluginRateLimiter.KeyFor(key.PluginId, ClientSubjectPrefix + key.ClientId.ToString(CultureInfo.InvariantCulture));
        using var lease = await _limiter.AcquireAsync(key.Budget, partition, ct);
        return lease.IsAcquired;
    }
}
