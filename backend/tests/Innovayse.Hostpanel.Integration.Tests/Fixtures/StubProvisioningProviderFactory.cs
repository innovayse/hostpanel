using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers;

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

    /// <summary>Every module the host knows: the double stands in for whichever panel a test seeds a server on.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Every module the host knows.</returns>
    public Task<IReadOnlyCollection<string>> SupportedModulesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyCollection<string>>(ServerModules.Known);

    /// <inheritdoc/>
    public IProvisioningProvider CreateFor(ServerConnectionDto server) => _provider;
}
