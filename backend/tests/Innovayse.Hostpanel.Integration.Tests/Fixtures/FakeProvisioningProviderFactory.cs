using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.Hostpanel.Infrastructure.Provisioning;

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
    /// <summary>The production factory's modules, so a test provisions onto the same servers production would.</summary>
    public IReadOnlyCollection<ServerModule> SupportedModules => ProvisioningProviderFactory.BuiltModules;

    /// <inheritdoc/>
    public IProvisioningProvider CreateFor(ServerConnectionDto server) => provider;
}
