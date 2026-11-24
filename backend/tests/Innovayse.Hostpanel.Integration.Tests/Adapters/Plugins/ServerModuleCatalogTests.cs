using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Servers.Common.Enums;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Infrastructure.Plugins;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="ServerModuleCatalog"/>: the host's module (cPanel) plus the ones installed provisioning plugins declare, each id once, and the
/// spelling to store for an id typed in any case.
/// </summary>
public sealed class ServerModuleCatalogTests
{
    /// <summary>The panel plugin class the manifests name; the catalog reads only manifests.</summary>
    private static readonly Type Implementation = typeof(FakePanelPlugin);

    /// <summary>With no plugin installed the catalog lists exactly the host's modules, all built in.</summary>
    [Fact]
    public void List_WithNoPlugin_IsTheHostsModules()
    {
        var catalog = new ServerModuleCatalog(PluginActivationTestSeeds.Registry());

        var options = catalog.List();

        options.Select(o => o.Id).Should().Equal(ServerModules.Listed);
        options.Should().OnlyContain(o => o.Source == ServerModuleSource.BuiltIn);
    }

    /// <summary>The plain Linux module is listed as built in next to cPanel, so an existing server can be added with it.</summary>
    [Fact]
    public void List_IncludesTheLinuxModuleAsBuiltIn()
    {
        var catalog = new ServerModuleCatalog(PluginActivationTestSeeds.Registry());

        var linux = catalog.List().Single(o => o.Id == ServerModules.Linux);

        linux.Source.Should().Be(ServerModuleSource.BuiltIn);
        catalog.Canonical("lInUx").Should().Be(ServerModules.Linux);
    }

    /// <summary>A plugin's modules are listed after the host's, under the plugin's name, and a module both declare is listed once in the host's spelling.</summary>
    [Fact]
    public void List_AddsPluginModules_AndMergesOnesTheHostKnows()
    {
        var plugin = PanelPluginHarness.Plugin("hestia", Implementation, ["hestiacp", "cpanel"], [PluginCapabilities.ProvisioningPanel]);
        var catalog = new ServerModuleCatalog(PluginActivationTestSeeds.Registry(plugin));

        var options = catalog.List();

        options.Select(o => o.Id).Should().Equal([.. ServerModules.Listed, "hestiacp"]);
        var hestia = options.Single(o => o.Id == "hestiacp");
        hestia.Name.Should().Be("Panel plugin");
        hestia.Source.Should().Be(ServerModuleSource.Plugin);
        var cpanel = options.Single(o => o.Id == ServerModules.CPanel);
        cpanel.Name.Should().Be("Panel plugin");
        cpanel.Source.Should().Be(ServerModuleSource.Plugin);
    }

    /// <summary>The spelling to store is the host's for a module it knows, the plugin's for one only a plugin declares, and nothing for an unknown id.</summary>
    [Fact]
    public void Canonical_AnswersTheSpellingToStore()
    {
        var plugin = PanelPluginHarness.Plugin("hestia", Implementation, ["HestiaCP"], [PluginCapabilities.ProvisioningPanel]);
        var catalog = new ServerModuleCatalog(PluginActivationTestSeeds.Registry(plugin));

        catalog.Canonical("cPaNeL").Should().Be(ServerModules.CPanel);
        catalog.Canonical("hestiacp").Should().Be("HestiaCP");
        catalog.Canonical("nobody").Should().BeNull();
    }

    /// <summary>The module a server is already stored with is accepted unchanged, in its stored spelling, even when nothing serves it, and only that one.</summary>
    [Fact]
    public void Canonical_AcceptsTheStoredModuleOfTheEditedServer()
    {
        var catalog = new ServerModuleCatalog(PluginActivationTestSeeds.Registry());

        catalog.Canonical("retiredpanel", "RetiredPanel").Should().Be("RetiredPanel");
        catalog.Canonical("retiredpanel").Should().BeNull();
        catalog.Canonical("nobody", "RetiredPanel").Should().BeNull();
    }

    /// <summary>A plugin's modules are the ones its own manifest declares, in its spelling, and another plugin's never come with them.</summary>
    [Fact]
    public void ModulesOf_AnswersTheModulesTheNamedPluginDeclares()
    {
        var hestia = PanelPluginHarness.Plugin("hestia", Implementation, ["HestiaCP", "hestia-eu"], [PluginCapabilities.ProvisioningPanel]);
        var other = PanelPluginHarness.Plugin("other", Implementation, ["othercp"], [PluginCapabilities.ProvisioningPanel]);
        var catalog = new ServerModuleCatalog(PluginActivationTestSeeds.Registry(hestia, other));

        catalog.ModulesOf("hestia").Should().Equal("HestiaCP", "hestia-eu");
        catalog.ModulesOf("other").Should().Equal("othercp");
    }

    /// <summary>A plugin that is not loaded, one with another id spelling and one that declares no provisioning capability serve no module.</summary>
    [Fact]
    public void ModulesOf_IsEmptyForAPluginThatServesNoServerModule()
    {
        var plain = PanelPluginHarness.Plugin("plain", Implementation, ["stray"], []);
        var panel = PanelPluginHarness.Plugin("hestia", Implementation, ["hestiacp"], [PluginCapabilities.ProvisioningPanel]);
        var catalog = new ServerModuleCatalog(PluginActivationTestSeeds.Registry(plain, panel));

        catalog.ModulesOf("plain").Should().BeEmpty();
        catalog.ModulesOf("missing").Should().BeEmpty();
        catalog.ModulesOf("HESTIA").Should().BeEmpty();
    }

    /// <summary>A plugin that declares no provisioning capability contributes no module, even when its manifest lists some.</summary>
    [Fact]
    public void List_IgnoresAPluginWithoutAProvisioningCapability()
    {
        var plugin = PanelPluginHarness.Plugin("plain", Implementation, ["stray"], []);
        var catalog = new ServerModuleCatalog(PluginActivationTestSeeds.Registry(plugin));

        catalog.List().Select(o => o.Id).Should().NotContain("stray");
    }
}
