using Innovayse.SDK.Plugins.Identity;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Resolves the one identity plugin an admin has switched on, which decides which client a signed-in
/// principal acts for. Implemented in Infrastructure with the same activation as every other plugin.
/// </summary>
public interface IIdentityPluginResolver
{
    /// <summary>Resolves the enabled identity plugin, if there is one.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The activated plugin, or <see langword="null"/> when no identity plugin is enabled (the host then
    /// applies its own rule) or the enabled one is missing a required setting.
    /// </returns>
    /// <exception cref="IdentityPluginConflictException">
    /// More than one identity plugin is enabled. The host never picks one of them, because each would map the
    /// same principal to a different client.
    /// </exception>
    Task<IClientResolver?> ResolveAsync(CancellationToken ct);
}
