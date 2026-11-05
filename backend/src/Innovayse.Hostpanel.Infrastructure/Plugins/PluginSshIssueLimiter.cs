using System.Globalization;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Infrastructure;

/*
 * The plugin SSH issue path is reached from inside a plugin call, after the route's own plugin tier was taken, so no
 * named policy can sit in front of it. It takes from the same in-process PluginRateLimiter the plugin routes use, under
 * the portal's ssh-keys budget, partitioned per plugin and client account ("client:" prefix, as the call filter keys a
 * client call). The lease is a sliding-window permit, released by the window, so it is disposed at once.
 *
 * The budget is per process, like every PluginRateLimiter tier: with several API instances each keeps its own window,
 * so a plugin can issue up to the budget times the number of instances. It is a flood guard, not the authority on how
 * many keys exist; the per-service key limit, checked under the service row's concurrency token, is that authority and
 * holds across instances. The supported deployment is one API instance.
 */

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/// <summary><see cref="IPluginSshIssueLimiter"/> over the host's <see cref="PluginRateLimiter"/>.</summary>
public sealed class PluginSshIssueLimiter : IPluginSshIssueLimiter
{
    /// <summary>The prefix of a client subject in a plugin partition key, as the plugin call filter writes it.</summary>
    private const string ClientSubjectPrefix = "client:";

    /// <summary>The host's plugin limiters, whose ssh-keys tier this takes from.</summary>
    private readonly PluginRateLimiter _limiter;

    /// <summary>Initializes the adapter.</summary>
    /// <param name="limiter">The host's plugin limiters, whose ssh-keys tier this takes from.</param>
    public PluginSshIssueLimiter(PluginRateLimiter limiter)
    {
        _limiter = limiter;
    }

    /// <inheritdoc/>
    public async Task<bool> TryAcquireAsync(string pluginId, int clientId, CancellationToken ct)
    {
        var key = PluginRateLimiter.KeyFor(pluginId, ClientSubjectPrefix + clientId.ToString(CultureInfo.InvariantCulture));
        using var lease = await _limiter.AcquireAsync(RateLimitPolicies.SshKeys, key, ct);
        return lease.IsAcquired;
    }
}
