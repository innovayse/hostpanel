using Innovayse.Hostpanel.Application.Admin.Servers.Common;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Creates <see cref="IProvisioningProvider"/> instances configured for a specific server.
/// Used when provisioning must target a particular server rather than a globally configured provider.
/// </summary>
public interface IProvisioningProviderFactory
{
    /// <summary>
    /// The server modules <see cref="CreateFor"/> can build a provider for: the host's own panels plus the modules of installed
    /// panel plugins that are switched on and configured. A product with no server group is provisioned onto a server of any of
    /// these modules, never one the factory would refuse.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The module ids a provider exists for.</returns>
    public Task<IReadOnlyCollection<string>> SupportedModulesAsync(CancellationToken ct);

    /// <summary>
    /// Creates a provisioning provider configured with the credentials and settings of the given server.
    /// </summary>
    /// <param name="server">The server to create the provider for.</param>
    /// <returns>A configured <see cref="IProvisioningProvider"/> instance.</returns>
    /// <exception cref="Innovayse.Hostpanel.Domain.DomainRuleException">
    /// Thrown with <c>PANEL_PLUGIN_CONFLICT</c> when two installed plugins declare the server's module, and with
    /// <c>PANEL_PLUGIN_UNAVAILABLE</c> when the module is not supported: no installed plugin serves it and the host has no provider for it.
    /// </exception>
    public IProvisioningProvider CreateFor(ServerConnectionDto server);
}
