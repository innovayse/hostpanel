using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.SDK.Plugins.Endpoints;
using Innovayse.SDK.Plugins.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginEndpointResolver"/>: only a loaded app plugin is found, and it is prepared only when it is switched
/// on and implements the endpoints contract, without anything of the plugin running until the host builds it, against
/// Postgres.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginEndpointResolverTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Only a loaded plugin whose manifest declares the app type is found; any other id or type is not.</summary>
    [Fact]
    public void Find_ReturnsOnlyALoadedAppPlugin()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var app = PluginActivationTestSeeds.NewId("app");
        var payment = PluginActivationTestSeeds.NewId("pay");
        var registry = PluginActivationTestSeeds.Registry(
            PluginActivationTestSeeds.Plugin(app, PluginType.App, typeof(EchoAppPlugin)),
            PluginActivationTestSeeds.Plugin(payment, PluginType.Payment, typeof(EchoAppPlugin)));
        var resolver = new PluginEndpointResolver(PluginActivationTestSeeds.Activator(factory, db), registry);

        resolver.Find(app)!.Id.Should().Be(app);
        resolver.Find(payment).Should().BeNull();
        resolver.Find("never-loaded").Should().BeNull();
    }

    /// <summary>A plugin that is off is not prepared, one that is on is, and preparing builds nothing: only calling the function does.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task PrepareAsync_ChecksTheSwitchWithoutBuildingThePlugin()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var on = PluginActivationTestSeeds.NewId("on");
        var off = PluginActivationTestSeeds.NewId("off");
        var registry = PluginActivationTestSeeds.Registry(
            PluginActivationTestSeeds.Plugin(on, PluginType.App, typeof(CountingAppPlugin)),
            PluginActivationTestSeeds.Plugin(off, PluginType.App, typeof(CountingAppPlugin)));
        await PluginActivationTestSeeds.EnableAsync(db, on);
        var resolver = new PluginEndpointResolver(PluginActivationTestSeeds.Activator(factory, db), registry);
        CountingAppPlugin.Built = 0;

        var disabled = await resolver.PrepareAsync(off, CancellationToken.None);
        var enabled = await resolver.PrepareAsync(on, CancellationToken.None);

        disabled.Should().BeNull();
        enabled.Should().NotBeNull();
        CountingAppPlugin.Built.Should().Be(0, "preparing must not run any plugin code, the constructor included");
        enabled!().Should().BeOfType<CountingAppPlugin>();
        CountingAppPlugin.Built.Should().Be(1);
    }

    /// <summary>A plugin that is off, a plugin that is not loaded and a plugin whose type is not the endpoints contract are all simply not prepared.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task PrepareAsync_AnUnusablePlugin_IsNull()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mistyped = PluginActivationTestSeeds.NewId("mistyped");
        var registry = PluginActivationTestSeeds.Registry(
            PluginActivationTestSeeds.Plugin(mistyped, PluginType.App, typeof(MistypedManifestPlugin)));
        await PluginActivationTestSeeds.EnableAsync(db, mistyped);
        var resolver = new PluginEndpointResolver(PluginActivationTestSeeds.Activator(factory, db), registry);

        (await resolver.PrepareAsync(mistyped, CancellationToken.None)).Should().BeNull();
        (await resolver.PrepareAsync("never-loaded", CancellationToken.None)).Should().BeNull();
    }

    /// <summary>An app plugin that counts how often it is built.</summary>
    private sealed class CountingAppPlugin : IPluginEndpoints
    {
        /// <summary>Gets or sets how many instances were built since a test last reset it.</summary>
        public static int Built { get; set; }

        /// <summary>Initializes the plugin, which counts itself.</summary>
        /// <param name="configuration">The plugin's own configuration, which the activator hands every plugin; unused here.</param>
        public CountingAppPlugin(Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            Built++;
        }

        /// <inheritdoc/>
        public Task<PluginReply> HandleAsync(PluginCall call, CancellationToken ct)
        {
            return Task.FromResult(PluginReply.NoContent());
        }
    }
}
