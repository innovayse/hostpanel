using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Test stub for <see cref="IProvisioningProviderFactory"/>. Hands back the same
/// <see cref="StubProvisioningProvider"/> for every server.
/// </summary>
/// <remarks>
/// Replacing <see cref="IProvisioningProvider"/> in the container is not enough on its
/// own: ProvisionServiceHandler does not resolve that interface, it asks this factory for
/// a provider built from the server row, and the real implementation always builds a hosting panel (v7)
/// client and calls the hostname on it. In a test that hostname belongs to a server that
/// does not exist, so without this stub provisioning fails at the network and the endpoint
/// answers 400.
/// </remarks>
public sealed class StubProvisioningProviderFactory : IProvisioningProviderFactory
{
    /// <summary>The single stub handed out for every server.</summary>
    private readonly StubProvisioningProvider _provider = new();

    /// <summary>The production factory's modules, so a test provisions onto the same servers production would.</summary>
    public IReadOnlyCollection<ServerModule> SupportedModules { get; } = [ServerModule.Cwp7];

    /// <inheritdoc/>
    public IProvisioningProvider CreateFor(ServerConnectionDto server) => _provider;
}
