using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Servers.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Infrastructure.Integrations.CPanel;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Provisioning;
using Innovayse.Hostpanel.Infrastructure.Provisioning.Settings;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// <see cref="ProvisioningProviderFactory"/> with panel plugins installed: a module a plugin serves gets the plugin provider (ahead of
/// a built in provider for the same panel, whatever the stored spelling), two serving plugins refuse, and a new order may pick only
/// the modules of plugins that are switched on and configured.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ProvisioningProviderFactoryPluginTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A server on a module a plugin serves gets the plugin provider.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CreateFor_ServedModule_IsThePluginProviderAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));

        var provider = harness.Factory().CreateFor(harness.Connection);

        provider.Should().BeOfType<PanelPluginProvisioningProvider>();
    }

    /// <summary>
    /// A server saved as <c>HestiaCP</c> is served by a plugin that declares <c>hestiacp</c>, whatever case the row holds:
    /// nothing stored is rewritten.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CreateFor_LegacyMixedCaseRow_IsServedByThePluginAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin), serverModule: "hestiacp");
        var legacy = new ServerConnectionDto(1, "legacy", "HestiaCP", "panel.example.test", null, "key");

        var provider = harness.Factory().CreateFor(legacy);

        provider.Should().BeOfType<PanelPluginProvisioningProvider>();
    }

    /// <summary>A module no plugin serves keeps its built in provider.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CreateFor_ModuleNoPluginServes_KeepsTheBuiltInProviderAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        var cpanel = new ServerConnectionDto(1, "cp", ServerModules.CPanel, "panel.example.test", null, null, "root", "whm-token");

        var provider = harness.Factory().CreateFor(cpanel);

        provider.Should().NotBeOfType<PanelPluginProvisioningProvider>();
    }

    /// <summary>Two plugins serving a module refuse every call with <c>PANEL_PLUGIN_CONFLICT</c>.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CreateFor_TwoPluginsServingTheModule_RefusesAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        var registry = PluginActivationTestSeeds.Registry(
            PanelPluginHarness.Plugin("first", typeof(FakePanelPlugin), [harness.Module], [PluginCapabilities.ProvisioningPanel]),
            PanelPluginHarness.Plugin("second", typeof(FakePanelPlugin), [harness.Module], [PluginCapabilities.ProvisioningPanel]));
        var doubled = new ServerModulePluginResolver(registry, NullLogger<ServerModulePluginResolver>.Instance);

        var act = () => harness.Factory(resolver: doubled).CreateFor(harness.Connection);

        act.Should().Throw<DomainRuleException>().Which.Code.Should().Be(HostpanelErrorCodes.PANEL_PLUGIN_CONFLICT);
    }

    /// <summary>The modules of a plugin that is switched on and configured are supported alongside the host's own.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SupportedModules_IncludeAnActivatedPluginsModulesAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));

        var supported = await harness.Factory().SupportedModulesAsync(CancellationToken.None);

        supported.Should().Contain(ProvisioningProviderFactory.BuiltModules).And.Contain(harness.Module);
    }

    /// <summary>A plugin that is switched off contributes no module: an order is never placed on a server its plugin would only refuse.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SupportedModules_ExcludeASwitchedOffPluginsModulesAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin), enabled: false);

        var supported = await harness.Factory().SupportedModulesAsync(CancellationToken.None);

        supported.Should().BeEquivalentTo(ProvisioningProviderFactory.BuiltModules);
    }

    /// <summary>The plain Linux module is never supported for orders, with or without an active plugin.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SupportedModules_NeverIncludeTheLinuxModuleAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));

        var supported = await harness.Factory().SupportedModulesAsync(CancellationToken.None);

        supported.Should().NotContain(ServerModules.Linux);
    }

    /// <summary>A module both the host and an active plugin serve is listed once.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SupportedModules_ListAModuleOnceWhenHostAndPluginBothServeItAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin), serverModule: "cpanel");

        var supported = await harness.Factory().SupportedModulesAsync(CancellationToken.None);

        supported.Count(module => ServerModules.Same(module, ServerModules.CPanel)).Should().Be(1);
    }

    /// <summary>With the development fake provider on, every known module is supported and the fake answers any server.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task FakeProvider_IsUnchangedByInstalledPluginsAsync()
    {
        using var harness = await PanelPluginHarness.CreateAsync(factory, typeof(FakePanelPlugin));
        var fake = harness.Factory(new ProvisioningOptions { UseFakeProvider = true });

        (await fake.SupportedModulesAsync(CancellationToken.None)).Should().BeEquivalentTo(ServerModules.Known);
        fake.CreateFor(harness.Connection).Should().BeOfType<NullCPanelProvisioningProvider>();
    }
}
