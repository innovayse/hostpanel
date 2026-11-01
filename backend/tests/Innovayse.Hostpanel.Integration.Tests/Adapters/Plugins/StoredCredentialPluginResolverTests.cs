using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.SDK.Plugins.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="StoredCredentialPluginResolver"/>: a payment plugin that opts in is offered its capability, and one
/// that does not, one that is switched off, and a module nobody installed have none, against Postgres.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class StoredCredentialPluginResolverTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Builds the resolver over the real payment plugin resolver, the way the host composes it.</summary>
    /// <param name="db">The data context the activator reads settings from; the caller keeps it alive.</param>
    /// <param name="plugin">The one plugin the registry holds.</param>
    /// <returns>The resolver under test.</returns>
    private StoredCredentialPluginResolver CreateResolver(AppDbContext db, LoadedPlugin plugin)
    {
        var payments = new PaymentPluginResolver(
            PluginActivationTestSeeds.Activator(factory, db),
            PluginActivationTestSeeds.Registry(plugin),
            NullLogger<PaymentPluginResolver>.Instance);
        return new StoredCredentialPluginResolver(payments);
    }

    /// <summary>An enabled payment plugin that implements the capability is offered it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ResolveAsync_EnabledPluginWithCapability_ReturnsTheCapability()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var module = PluginActivationTestSeeds.NewId("stored-pay");
        await PluginActivationTestSeeds.EnableAsync(db, module);
        var resolver = CreateResolver(db, PluginActivationTestSeeds.Plugin(module, PluginType.Payment, typeof(StoredCredentialCapablePaymentPlugin)));

        var capability = await resolver.ResolveAsync(module, CancellationToken.None);

        capability.Should().BeOfType<StoredCredentialCapablePaymentPlugin>();
    }

    /// <summary>An enabled payment plugin that does not implement the capability has none.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ResolveAsync_EnabledPluginWithoutCapability_ReturnsNull()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var module = PluginActivationTestSeeds.NewId("plain-pay");
        await PluginActivationTestSeeds.EnableAsync(db, module);
        var resolver = CreateResolver(db, PluginActivationTestSeeds.Plugin(module, PluginType.Payment, typeof(ConfigurationCapturingPaymentPlugin)));

        (await resolver.ResolveAsync(module, CancellationToken.None)).Should().BeNull();
    }

    /// <summary>A plugin with the capability that the admin has not enabled has none, so a disabled gateway is never charged.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ResolveAsync_DisabledPluginWithCapability_ReturnsNull()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var module = PluginActivationTestSeeds.NewId("off-pay");
        var resolver = CreateResolver(db, PluginActivationTestSeeds.Plugin(module, PluginType.Payment, typeof(StoredCredentialCapablePaymentPlugin)));

        (await resolver.ResolveAsync(module, CancellationToken.None)).Should().BeNull();
    }

    /// <summary>A module that is not installed has no capability.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ResolveAsync_UnknownModule_ReturnsNull()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var module = PluginActivationTestSeeds.NewId("stored-pay");
        var resolver = CreateResolver(db, PluginActivationTestSeeds.Plugin(module, PluginType.Payment, typeof(StoredCredentialCapablePaymentPlugin)));

        (await resolver.ResolveAsync("not-installed", CancellationToken.None)).Should().BeNull();
    }
}
