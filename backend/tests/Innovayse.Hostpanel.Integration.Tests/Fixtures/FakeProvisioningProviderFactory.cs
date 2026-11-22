using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;

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
    /// <summary>Every module the tests seed servers on: the double stands in for whichever panel that is.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The host's modules and the second test panel.</returns>
    public Task<IReadOnlyCollection<string>> SupportedModulesAsync(CancellationToken ct) =>
        Task.FromResult(TestPanelModules.Supported);

    /// <inheritdoc/>
    public IProvisioningProvider CreateFor(ServerConnectionDto server) => provider;
}
