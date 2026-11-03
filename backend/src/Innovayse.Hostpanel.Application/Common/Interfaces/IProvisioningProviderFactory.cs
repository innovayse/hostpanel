using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Domain.Servers.Enums;

namespace Innovayse.Hostpanel.Application.Common.Interfaces;

/// <summary>
/// Creates <see cref="IProvisioningProvider"/> instances configured for a specific server.
/// Used when provisioning must target a particular server rather than a globally configured provider.
/// </summary>
public interface IProvisioningProviderFactory
{
    /// <summary>
    /// The server modules <see cref="CreateFor"/> can build a provider for. A product with no server
    /// group is provisioned onto a server of any of these modules, never one the factory would refuse.
    /// </summary>
    IReadOnlyCollection<ServerModule> SupportedModules { get; }

    /// <summary>
    /// Creates a provisioning provider configured with the credentials and settings of the given server.
    /// </summary>
    /// <param name="server">The server to create the provider for.</param>
    /// <returns>A configured <see cref="IProvisioningProvider"/> instance.</returns>
    /// <exception cref="System.NotSupportedException">
    /// Thrown when the server's <see cref="ServerConnectionDto.Module"/> is not supported.
    /// </exception>
    IProvisioningProvider CreateFor(ServerConnectionDto server);
}
