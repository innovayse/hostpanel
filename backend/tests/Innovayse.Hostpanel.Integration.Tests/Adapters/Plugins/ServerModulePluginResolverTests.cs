using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Servers.Common.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="ServerModulePluginResolver"/>: a server's module id is matched ignoring case to the installed provisioning plugin that
/// declares it for the capability asked; a module nobody declares is not served; two plugins declaring it are a conflict and none is
/// chosen. It never looks at whether a plugin is switched on.
/// </summary>
public sealed class ServerModulePluginResolverTests
{
    /// <summary>The panel plugin class the manifests name; the resolver reads only manifests.</summary>
    private static readonly Type Implementation = typeof(FakePanelPlugin);

    /// <summary>A module a plugin declares in lower case finds that plugin for a server stored in the older mixed case spelling.</summary>
    [Fact]
    public void Find_MatchesTheModuleIgnoringCase()
    {
        var plugin = PanelPluginHarness.Plugin("hestia-plugin", Implementation, ["hestiacp"], [PluginCapabilities.ProvisioningPanel]);
        var resolver = ResolverOver(plugin);

        var match = resolver.Find("HestiaCP", PluginCapabilities.ProvisioningPanel);

        match.Outcome.Should().Be(ServerModulePluginOutcome.Served);
        match.PluginId.Should().Be("hestia-plugin");
    }

    /// <summary>A module no installed plugin declares is not served, so the host's own provider, if any, handles it.</summary>
    [Fact]
    public void Find_ModuleNobodyDeclares_IsNotServed()
    {
        var resolver = ResolverOver(PanelPluginHarness.Plugin("a", Implementation, ["hestiacp"], [PluginCapabilities.ProvisioningPanel]));

        var match = resolver.Find("cpanel", PluginCapabilities.ProvisioningPanel);

        match.Outcome.Should().Be(ServerModulePluginOutcome.NotServed);
        match.PluginId.Should().BeNull();
    }

    /// <summary>A plugin that declares the module but not the capability asked does not serve it.</summary>
    [Fact]
    public void Find_PluginWithoutTheCapability_IsNotServed()
    {
        var resolver = ResolverOver(PanelPluginHarness.Plugin("vm", Implementation, ["hestiacp"], [PluginCapabilities.VirtualServer]));

        resolver.Find("hestiacp", PluginCapabilities.ProvisioningPanel).Outcome.Should().Be(ServerModulePluginOutcome.NotServed);
        resolver.Find("hestiacp", PluginCapabilities.VirtualServer).Outcome.Should().Be(ServerModulePluginOutcome.Served);
    }

    /// <summary>Two plugins declaring one module are a conflict: neither is chosen.</summary>
    [Fact]
    public void Find_TwoPluginsDeclaringTheModule_IsAConflict()
    {
        var resolver = ResolverOver(
            PanelPluginHarness.Plugin("one", Implementation, ["hestiacp"], [PluginCapabilities.ProvisioningPanel]),
            PanelPluginHarness.Plugin("two", Implementation, ["HESTIACP"], [PluginCapabilities.ProvisioningPanel]));

        var match = resolver.Find("hestiacp", PluginCapabilities.ProvisioningPanel);

        match.Outcome.Should().Be(ServerModulePluginOutcome.Conflict);
        match.PluginId.Should().BeNull();
    }

    /// <summary>Builds a resolver over a registry holding the plugins.</summary>
    /// <param name="plugins">The installed plugins.</param>
    /// <returns>The resolver.</returns>
    private static ServerModulePluginResolver ResolverOver(params LoadedPlugin[] plugins)
    {
        return new ServerModulePluginResolver(PluginActivationTestSeeds.Registry(plugins), NullLogger<ServerModulePluginResolver>.Instance);
    }
}
