using Hostpanel.Sdk.Plugins.Provisioning;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Provisioning;

namespace Innovayse.Hostpanel.Architecture.Tests;

/// <summary>
/// A hosting panel plugin is reached through the host's own provisioning port: only the plugin adapters name the SDK's panel contracts,
/// so no handler, controller or entity knows whether a panel is driven by the host or by a plugin.
/// </summary>
public sealed class PanelPluginPortTests
{
    /// <summary>The SDK contracts a hosting panel plugin implements.</summary>
    private static readonly Type[] PanelContracts =
    [
        typeof(IProvisioningPanelPlugin), typeof(IProvisioningPanelLoginPlugin), typeof(IProvisioningPanelPackagesPlugin),
    ];

    /// <summary>The namespaces whose types may name a panel contract: the plugin runtime and the provisioning adapters built on it.</summary>
    private static readonly string[] AllowedNamespaces =
    [
        "Innovayse.Hostpanel.Infrastructure.Plugins",
        "Innovayse.Hostpanel.Infrastructure.Provisioning",
    ];

    /// <summary>
    /// No type outside <c>Infrastructure/Plugins</c> and <c>Infrastructure/Provisioning</c> names a panel contract in a signature or
    /// calls one of its methods.
    /// </summary>
    [Fact]
    public void OnlyThePluginAdapters_NameAPanelPluginContract()
    {
        var offenders = new[]
            {
                Layers.SharedKernel, Layers.Domain, Layers.Shared, Layers.Application, Layers.Infrastructure, Layers.Api,
            }
            .SelectMany(Layers.AuthoredTypesOf)
            .Where(type => !AllowedNamespaces.Any(allowed => type.Namespace?.StartsWith(allowed, StringComparison.Ordinal) == true))
            .Where(type => TypeSignatures.TypesNamedBy(type).Any(PanelContracts.Contains)
                || MethodBodyReader.CallsMadeBy(type).Any(call => call.DeclaringType is { } declaring && PanelContracts.Contains(declaring)))
            .Select(type => type.FullName)
            .ToList();

        offenders.Should().BeEmpty("a panel plugin is reached through IProvisioningProvider, IProvisioningProviderFactory and IServerConnectionTester");
    }

    /// <summary>The adapter a handler gets for a plugin served module implements the same port as the host's own panels.</summary>
    [Fact]
    public void ThePluginProvider_IsAnIProvisioningProvider()
    {
        typeof(IProvisioningProvider).IsAssignableFrom(typeof(PanelPluginProvisioningProvider)).Should().BeTrue();
    }

    /// <summary>The one place a plugin's module is mapped to the plugin for a call is the resolver port, with an adapter in Infrastructure.</summary>
    [Fact]
    public void TheModuleToPluginMapping_IsAPortWithAnInfrastructureAdapter()
    {
        var ports = PortPlacementTests.Ports();

        ports.Should().Contain(typeof(IServerModulePluginResolver)).And.Contain(typeof(IServerModuleCatalog));
    }
}
