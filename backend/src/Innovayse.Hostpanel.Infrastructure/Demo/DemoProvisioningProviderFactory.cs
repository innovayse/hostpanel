using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers;

namespace Innovayse.Hostpanel.Infrastructure.Demo;

/// <summary>Hands out the demo provisioning provider for any server.</summary>
public sealed class DemoProvisioningProviderFactory : IProvisioningProviderFactory
{
    /// <summary>The one fake provider every server resolves to.</summary>
    private readonly IProvisioningProvider _provider;

    /// <summary>Creates the factory.</summary>
    /// <param name="provider">The one fake provider every server resolves to.</param>
    public DemoProvisioningProviderFactory(IProvisioningProvider provider)
    {
        _provider = provider;
    }

    /// <summary>The demo's fake provider stands in for every panel, so a server on any module will do.</summary>
    /// <param name="ct">Unused; the list is known without a call.</param>
    /// <returns>Every known panel module.</returns>
    public Task<IReadOnlyCollection<string>> SupportedModulesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyCollection<string>>(ServerModules.Known);

    /// <inheritdoc/>
    public IProvisioningProvider CreateFor(ServerConnectionDto server) => _provider;
}
