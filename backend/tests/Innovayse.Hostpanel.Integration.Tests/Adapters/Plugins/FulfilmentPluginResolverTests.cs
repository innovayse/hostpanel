using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.SDK.Plugins.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="FulfilmentPluginResolver"/>: every enabled fulfilment plugin is resolved with its own settings,
/// and a plugin that is off, misconfigured, of another type or not a fulfilment plugin at all is left out,
/// against Postgres.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class FulfilmentPluginResolverTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Of a mix of plugins only the enabled, fully configured fulfilment ones come back, in plugin id order,
    /// each with its own settings in its configuration.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ResolveAllAsync_ReturnsEveryEnabledFulfilmentPluginInIdOrder()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var first = PluginActivationTestSeeds.NewId("a-fulfil");
        var second = PluginActivationTestSeeds.NewId("b-fulfil");
        var disabled = PluginActivationTestSeeds.NewId("c-fulfil");
        var unconfigured = PluginActivationTestSeeds.NewId("d-fulfil");
        var payment = PluginActivationTestSeeds.NewId("e-pay");
        var mistyped = PluginActivationTestSeeds.NewId("f-fulfil");
        var registry = PluginActivationTestSeeds.Registry(
            PluginActivationTestSeeds.Plugin(second, PluginType.Fulfilment, typeof(ConfigurationCapturingFulfilmentPlugin), requiresApiKey: true),
            PluginActivationTestSeeds.Plugin(first, PluginType.Fulfilment, typeof(ConfigurationCapturingFulfilmentPlugin)),
            PluginActivationTestSeeds.Plugin(disabled, PluginType.Fulfilment, typeof(ConfigurationCapturingFulfilmentPlugin)),
            PluginActivationTestSeeds.Plugin(unconfigured, PluginType.Fulfilment, typeof(ConfigurationCapturingFulfilmentPlugin), requiresApiKey: true),
            PluginActivationTestSeeds.Plugin(payment, PluginType.Payment, typeof(ConfigurationCapturingPaymentPlugin)),
            PluginActivationTestSeeds.Plugin(mistyped, PluginType.Fulfilment, typeof(MistypedManifestPlugin)));
        await PluginActivationTestSeeds.EnableAsync(db, first, second, unconfigured, payment, mistyped);
        await PluginActivationTestSeeds.StoreAsync(db, second, PluginActivationTestSeeds.ApiKeyField, "k-second");
        var resolver = new FulfilmentPluginResolver(PluginActivationTestSeeds.Activator(factory, db), registry);

        var plugins = await resolver.ResolveAllAsync(CancellationToken.None);

        plugins.Select(plugin => plugin.PluginId).Should().Equal(first, second);
        var configured = plugins[1].Plugin.Should().BeOfType<ConfigurationCapturingFulfilmentPlugin>().Subject;
        configured.Configuration[IntegrationSettingKeys.FieldKey(second, PluginActivationTestSeeds.ApiKeyField)].Should().Be("k-second");
    }

    /// <summary>With no fulfilment plugin loaded the list is empty, never null.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ResolveAllAsync_NoFulfilmentPluginLoaded_ReturnsEmpty()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var resolver = new FulfilmentPluginResolver(PluginActivationTestSeeds.Activator(factory, db), PluginActivationTestSeeds.Registry());

        var plugins = await resolver.ResolveAllAsync(CancellationToken.None);

        plugins.Should().BeEmpty();
    }
}
