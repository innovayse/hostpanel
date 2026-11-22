using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// An <see cref="IProvisioningProviderFactory"/> that hands back one fixed
/// <see cref="FakeProvisioningProvider"/> regardless of the server asked for, so a test can flip
/// that one instance's <see cref="FakeProvisioningProvider.ShouldFail"/> and see every handler
/// that resolves a provider through this factory answer the same way.
/// </summary>
/// <param name="provider">The provider every server resolves to.</param>
public sealed class FakeProvisioningProviderFactory(FakeProvisioningProvider provider) : IProvisioningProviderFactory
{
    /// <summary>Every module the host knows: the double stands in for whichever panel a test seeds a server on.</summary>
    public Task<IReadOnlyCollection<string>> SupportedModulesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyCollection<string>>(ServerModules.Known);

    /// <inheritdoc/>
    public IProvisioningProvider CreateFor(ServerConnectionDto server) => provider;
}
