using Innovayse.Hostpanel.Application.Services.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The budget of host accesses a plugin may be granted for one client, for example the portal's <c>ssh-keys</c> tier
/// (<c>RateLimit:SshKeys</c>), counted per plugin and client account, so a plugin cannot churn accesses faster than the
/// client's own portal could. Fails closed: when the budget is spent, nothing is generated or installed.
/// </summary>
public interface IPluginHostAccessLimiter
{
    /// <summary>Takes one access from the plugin's budget for the client.</summary>
    /// <param name="key">The plugin, the client and the budget it is counted in.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the access may go ahead; <see langword="false"/> when the budget is spent.</returns>
    public Task<bool> TryAcquireAsync(PluginHostAccessKey key, CancellationToken ct);
}
