using Innovayse.Hostpanel.Application.Plugins.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The budget of host accesses a plugin may be granted for one subject, for example the portal's <c>ssh-keys</c> tier
/// (<c>RateLimit:SshKeys</c>) per client account or the <c>plugin-order-read</c> tier per gateway order, so a plugin
/// cannot churn accesses faster than the client's own portal could. Fails closed: when the budget is spent, nothing is
/// generated, installed or read.
/// </summary>
public interface IPluginHostAccessLimiter
{
    /// <summary>Takes one access from the plugin's budget for the subject.</summary>
    /// <param name="key">The plugin, the subject and the budget it is counted in.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the access may go ahead; <see langword="false"/> when the budget is spent.</returns>
    public Task<bool> TryAcquireAsync(PluginHostAccessKey key, CancellationToken ct);
}
