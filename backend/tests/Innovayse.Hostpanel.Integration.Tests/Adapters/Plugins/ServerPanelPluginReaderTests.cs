using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="ServerPanelPluginReader"/>: names the panel plugin that serves a module, with its display name and whether it ships an
/// icon, and nothing for a module no plugin serves or two plugins claim.
/// </summary>
public sealed class ServerPanelPluginReaderTests
{
    /// <summary>The panel plugin class the manifests name; the reader looks only at manifests.</summary>
    private static readonly Type Implementation = typeof(FakePanelPlugin);

    /// <summary>A plugin that serves the module is named with its display name, and an id typed in any case matches.</summary>
    [Fact]
    public void Find_ServedModule_NamesThePlugin()
    {
        var reader = ReaderOver(PanelPluginHarness.Plugin("hestia", Implementation, ["hestiacp"], [PluginCapabilities.ProvisioningPanel]));

        var plugin = reader.Find("HestiaCP");

        plugin.Should().Be(new ServicePanelPluginDto("hestia", "Panel plugin", null));
    }

    /// <summary>A plugin that ships an icon reports its version, so a screen knows the icon route will answer and can version the address.</summary>
    [Fact]
    public void Find_PluginWithIcon_ReportsIt()
    {
        var withIcon = PanelPluginHarness.Plugin("hestia", Implementation, ["hestiacp"], [PluginCapabilities.ProvisioningPanel])
            with { Icon = new PluginIconDto("image/svg+xml", [1], "\"tag\"") };
        var reader = ReaderOver(withIcon);

        reader.Find("hestiacp")!.IconVersion.Should().Be(withIcon.Manifest.Version);
    }

    /// <summary>The module the host provisions itself, with no plugin declaring it, has no serving plugin.</summary>
    [Fact]
    public void Find_BuiltInModuleNoPluginServes_IsNull()
    {
        var reader = ReaderOver(PanelPluginHarness.Plugin("hestia", Implementation, ["hestiacp"], [PluginCapabilities.ProvisioningPanel]));

        reader.Find(ServerModules.CPanel).Should().BeNull();
    }

    /// <summary>A plugin that declares the module but not the panel capability does not serve it.</summary>
    [Fact]
    public void Find_PluginWithoutThePanelCapability_IsNull()
    {
        var reader = ReaderOver(PanelPluginHarness.Plugin("plain", Implementation, ["hestiacp"], []));

        reader.Find("hestiacp").Should().BeNull();
    }

    /// <summary>Two plugins claiming one module are a conflict and none is named.</summary>
    [Fact]
    public void Find_TwoPluginsClaimTheModule_IsNull()
    {
        var reader = ReaderOver(
            PanelPluginHarness.Plugin("first", Implementation, ["hestiacp"], [PluginCapabilities.ProvisioningPanel]),
            PanelPluginHarness.Plugin("second", Implementation, ["hestiacp"], [PluginCapabilities.ProvisioningPanel]));

        reader.Find("hestiacp").Should().BeNull();
    }

    /// <summary>Builds the reader over a registry holding the plugins, with the real resolver.</summary>
    /// <param name="plugins">The loaded plugins.</param>
    /// <returns>The reader.</returns>
    private static ServerPanelPluginReader ReaderOver(params LoadedPlugin[] plugins)
    {
        var registry = PluginActivationTestSeeds.Registry(plugins);
        return new ServerPanelPluginReader(new ServerModulePluginResolver(registry, NullLogger<ServerModulePluginResolver>.Instance), registry);
    }
}
