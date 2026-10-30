using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.SDK.Plugins.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="IdentityPluginResolver"/>: no enabled identity plugin resolves to nothing, exactly one resolves to
/// it, and more than one is refused rather than one of them picked, against Postgres.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class IdentityPluginResolverTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>With no identity plugin loaded the host keeps its own rule: the resolver answers null.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ResolveAsync_NoIdentityPluginLoaded_ReturnsNull()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var resolver = new IdentityPluginResolver(PluginActivationTestSeeds.Activator(factory, db), PluginActivationTestSeeds.Registry());

        var plugin = await resolver.ResolveAsync(CancellationToken.None);

        plugin.Should().BeNull();
    }

    /// <summary>Identity plugins that are loaded but all switched off resolve to nothing too.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ResolveAsync_OnlyDisabledIdentityPlugins_ReturnsNull()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var registry = PluginActivationTestSeeds.Registry(
            PluginActivationTestSeeds.Plugin(PluginActivationTestSeeds.NewId("identity"), PluginType.Identity, typeof(ConfigurationCapturingClientResolver)),
            PluginActivationTestSeeds.Plugin(PluginActivationTestSeeds.NewId("identity"), PluginType.Identity, typeof(ConfigurationCapturingClientResolver)));
        var resolver = new IdentityPluginResolver(PluginActivationTestSeeds.Activator(factory, db), registry);

        var plugin = await resolver.ResolveAsync(CancellationToken.None);

        plugin.Should().BeNull();
    }

    /// <summary>Exactly one enabled identity plugin among disabled ones is the one resolved.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ResolveAsync_OneEnabledIdentityPlugin_ReturnsIt()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var enabled = PluginActivationTestSeeds.NewId("identity");
        var registry = PluginActivationTestSeeds.Registry(
            PluginActivationTestSeeds.Plugin(enabled, PluginType.Identity, typeof(ConfigurationCapturingClientResolver)),
            PluginActivationTestSeeds.Plugin(PluginActivationTestSeeds.NewId("identity"), PluginType.Identity, typeof(ConfigurationCapturingClientResolver)));
        await PluginActivationTestSeeds.EnableAsync(db, enabled);
        var resolver = new IdentityPluginResolver(PluginActivationTestSeeds.Activator(factory, db), registry);

        var plugin = await resolver.ResolveAsync(CancellationToken.None);

        var resolved = plugin.Should().BeOfType<ConfigurationCapturingClientResolver>().Subject;
        resolved.Configuration[IntegrationSettingKeys.EnabledKey(enabled)].Should().Be("true");
    }

    /// <summary>Two enabled identity plugins are a configuration error naming both, never a silent pick.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ResolveAsync_TwoEnabledIdentityPlugins_ThrowsNamingBoth()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var first = PluginActivationTestSeeds.NewId("a-identity");
        var second = PluginActivationTestSeeds.NewId("b-identity");
        var registry = PluginActivationTestSeeds.Registry(
            PluginActivationTestSeeds.Plugin(second, PluginType.Identity, typeof(ConfigurationCapturingClientResolver)),
            PluginActivationTestSeeds.Plugin(first, PluginType.Identity, typeof(ConfigurationCapturingClientResolver)));
        await PluginActivationTestSeeds.EnableAsync(db, first, second);
        var resolver = new IdentityPluginResolver(PluginActivationTestSeeds.Activator(factory, db), registry);

        var resolve = () => resolver.ResolveAsync(CancellationToken.None);

        var refused = await resolve.Should().ThrowAsync<IdentityPluginConflictException>();
        refused.Which.PluginIds.Should().Equal(first, second);
    }

    /// <summary>
    /// A second enabled identity plugin still conflicts while it lacks a required setting: which plugin answers
    /// must not depend on which of two switched-on plugins happens to be fully configured.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ResolveAsync_SecondEnabledPluginMissingASetting_StillThrows()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var configured = PluginActivationTestSeeds.NewId("a-identity");
        var unconfigured = PluginActivationTestSeeds.NewId("b-identity");
        var registry = PluginActivationTestSeeds.Registry(
            PluginActivationTestSeeds.Plugin(configured, PluginType.Identity, typeof(ConfigurationCapturingClientResolver)),
            PluginActivationTestSeeds.Plugin(unconfigured, PluginType.Identity, typeof(ConfigurationCapturingClientResolver), requiresApiKey: true));
        await PluginActivationTestSeeds.EnableAsync(db, configured, unconfigured);
        var resolver = new IdentityPluginResolver(PluginActivationTestSeeds.Activator(factory, db), registry);

        var resolve = () => resolver.ResolveAsync(CancellationToken.None);

        await resolve.Should().ThrowAsync<IdentityPluginConflictException>();
    }
}
