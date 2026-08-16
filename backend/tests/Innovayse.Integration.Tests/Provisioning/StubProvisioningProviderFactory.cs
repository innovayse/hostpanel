namespace Innovayse.Integration.Tests.Provisioning;

using Innovayse.Domain.Provisioning.Interfaces;
using Innovayse.Domain.Servers;

/// <summary>
/// Test stub for <see cref="IProvisioningProviderFactory"/>. Hands back the same
/// <see cref="StubProvisioningProvider"/> for every server.
/// </summary>
/// <remarks>
/// Replacing <see cref="IProvisioningProvider"/> in the container was not enough on its
/// own: ProvisionServiceHandler does not resolve that interface, it asks this factory for
/// a provider built from the server row, and the real implementation always builds a CWP7
/// client and calls the hostname on it. In a test that hostname belongs to a server that
/// does not exist, so provisioning failed at the network and the endpoint answered 400 —
/// the registered stub sitting unused beside it the whole time.
/// </remarks>
internal sealed class StubProvisioningProviderFactory : IProvisioningProviderFactory
{
    private readonly StubProvisioningProvider _provider = new();

    /// <inheritdoc/>
    public IProvisioningProvider CreateFor(Server server) => _provider;
}
