namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// The budget of SSH identities a plugin may have issued for one client: the portal's <c>ssh-keys</c> tier
/// (<c>RateLimit:SshKeys</c>), counted per plugin and client account, so a plugin cannot churn keys faster than the
/// client's own portal could. Fails closed: when the budget is spent, nothing is generated or installed.
/// </summary>
public interface IPluginSshIssueLimiter
{
    /// <summary>Takes one issue from the plugin's budget for the client.</summary>
    /// <param name="pluginId">The plugin asking.</param>
    /// <param name="clientId">The client the call acts for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the issue may go ahead; <see langword="false"/> when the budget is spent.</returns>
    Task<bool> TryAcquireAsync(string pluginId, int clientId, CancellationToken ct);
}
