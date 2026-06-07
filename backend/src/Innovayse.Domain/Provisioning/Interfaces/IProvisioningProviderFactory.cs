namespace Innovayse.Domain.Provisioning.Interfaces;

using Innovayse.Domain.Servers;

/// <summary>
/// Creates <see cref="IProvisioningProvider"/> instances configured for a specific server.
/// Used when provisioning must target a particular server rather than a globally configured provider.
/// </summary>
public interface IProvisioningProviderFactory
{
    /// <summary>
    /// Creates a provisioning provider configured with the credentials and settings of the given server.
    /// </summary>
    /// <param name="server">The server to create the provider for.</param>
    /// <returns>A configured <see cref="IProvisioningProvider"/> instance.</returns>
    /// <exception cref="System.NotSupportedException">
    /// Thrown when the server's <see cref="Server.Module"/> is not supported.
    /// </exception>
    IProvisioningProvider CreateFor(Server server);
}
