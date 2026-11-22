using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Admin.Integrations.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginStatusProbe"/> for a registrar plugin: Test Connection runs the plugin's own connection test with the
/// connection built from its saved settings, a failed test reports the plugin as unavailable, and a plugin that is not
/// switched on, throws or does not answer in time is reported as such, as for any other plugin.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginStatusProbeRegistrarTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Builds the probe over a registry holding the given plugin.</summary>
    /// <param name="db">The data context the activator reads the settings from.</param>
    /// <param name="plugin">The plugin the registry holds.</param>
    /// <returns>The probe under test.</returns>
    private PluginStatusProbe Probe(AppDbContext db, LoadedPlugin plugin)
    {
        var registry = PluginActivationTestSeeds.Registry(plugin);

        return new PluginStatusProbe(
            PluginActivationTestSeeds.Activator(factory, db, registry: registry),
            registry,
            Options.Create(new PluginApiOptions()),
            NullLogger<PluginStatusProbe>.Instance);
    }

    /// <summary>Builds a registrar that declares the operations capability and answers its connection test as its settings say.</summary>
    /// <param name="id">The plugin id.</param>
    /// <returns>The loaded plugin.</returns>
    private static LoadedPlugin Configured(string id)
    {
        return RegistrarRegistryTestSeeds.Plugin(
            id, PluginType.Registrar, typeof(ConfiguredOutcomeRegistrarPlugin), PluginCapabilities.RegistrarOperations);
    }

    /// <summary>Stores the settings that make a registrar from <see cref="RegistrarRegistryTestSeeds"/> configured and switches it on.</summary>
    /// <param name="db">The data context.</param>
    /// <param name="id">The plugin id.</param>
    /// <returns>A task that completes when the settings are stored.</returns>
    private static async Task ConfigureAsync(AppDbContext db, string id)
    {
        await PluginActivationTestSeeds.EnableAsync(db, id);
        await PluginActivationTestSeeds.StoreAsync(db, id, RegistrarRegistryTestSeeds.AccountField, "account-1");
        await PluginActivationTestSeeds.StoreAsync(db, id, RegistrarRegistryTestSeeds.KeyField, "key-1");
    }

    /// <summary>A registrar plugin that offers operations can be tested, one that does not cannot.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Supports_OnlyARegistrarThatOffersOperationsAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = PluginActivationTestSeeds.NewId("reg");
        var markerId = PluginActivationTestSeeds.NewId("marker");
        var registry = PluginActivationTestSeeds.Registry(
            RegistrarRegistryTestSeeds.OperationsOnlyRegistrar(id),
            RegistrarRegistryTestSeeds.Plugin(markerId, PluginType.Registrar, typeof(ScriptedRegistrarPlugin)));
        var probe = new PluginStatusProbe(
            PluginActivationTestSeeds.Activator(factory, db, registry: registry), registry, Options.Create(new PluginApiOptions()), NullLogger<PluginStatusProbe>.Instance);

        probe.Supports(id).Should().BeTrue();
        probe.Supports(markerId).Should().BeFalse();
        probe.Supports("no-such-plugin").Should().BeFalse();
    }

    /// <summary>A connection the plugin confirms is reported healthy.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Connected_IsReportedHealthyAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = PluginActivationTestSeeds.NewId("ok");
        await ConfigureAsync(db, id);

        var result = await Probe(db, RegistrarRegistryTestSeeds.FullRegistrar(id)).ProbeAsync(id, CancellationToken.None);

        result.Outcome.Should().Be(PluginStatusProbeOutcome.Reported);
        result.Health.Should().Be(PluginHealth.Healthy);
    }

    /// <summary>A connection the plugin cannot confirm, whatever the reason, is reported unavailable.</summary>
    /// <param name="outcome">What the plugin's connection test answered.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(PanelConnectionOutcome.NotConfigured)]
    [InlineData(PanelConnectionOutcome.AuthenticationFailed)]
    [InlineData(PanelConnectionOutcome.Unreachable)]
    public async Task ConnectionNotConfirmed_IsReportedUnavailableAsync(PanelConnectionOutcome outcome)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = PluginActivationTestSeeds.NewId("down");
        await ConfigureAsync(db, id);
        await PluginActivationTestSeeds.StoreAsync(db, id, ConfiguredOutcomeRegistrarPlugin.OutcomeKey, outcome.ToString());

        var result = await Probe(db, Configured(id)).ProbeAsync(id, CancellationToken.None);

        result.Outcome.Should().Be(PluginStatusProbeOutcome.Reported);
        result.Health.Should().Be(PluginHealth.Unavailable);
    }

    /// <summary>A plugin whose connection test throws is reported failed, never as a thrown exception.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ConnectionTestThrows_IsFailedAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = PluginActivationTestSeeds.NewId("boom");
        await ConfigureAsync(db, id);
        await PluginActivationTestSeeds.StoreAsync(db, id, ConfiguredOutcomeRegistrarPlugin.OutcomeKey, ConfiguredOutcomeRegistrarPlugin.ThrowValue);

        var result = await Probe(db, Configured(id)).ProbeAsync(id, CancellationToken.None);

        result.Outcome.Should().Be(PluginStatusProbeOutcome.Failed);
    }

    /// <summary>A registrar plugin the admin did not switch on is reported inactive and the plugin is not called.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task NotSwitchedOn_IsInactiveAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = PluginActivationTestSeeds.NewId("idle");

        var result = await Probe(db, RegistrarRegistryTestSeeds.FullRegistrar(id)).ProbeAsync(id, CancellationToken.None);

        result.Outcome.Should().Be(PluginStatusProbeOutcome.Inactive);
    }
}
