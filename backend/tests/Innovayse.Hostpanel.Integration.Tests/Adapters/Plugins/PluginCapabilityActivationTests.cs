using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Infrastructure.Plugins.Enums;
using Innovayse.SDK.Plugins.Endpoints;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Fulfilment;
using Innovayse.SDK.Plugins.Manifest;
using Innovayse.SDK.Plugins.Payment;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="PluginActivator"/> and <see cref="PluginEndpointResolver"/> with the <c>endpoints</c> capability (#818),
/// against Postgres: a fulfilment plugin declaring it is found and prepared for routes and still activates for
/// fulfilment; one whose class does not implement <see cref="IPluginEndpoints"/> is refused at activation for every
/// contract; one without the capability is not found.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginCapabilityActivationTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A fulfilment plugin declaring endpoints serves routes and still activates as a fulfilment plugin.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task FulfilmentWithEndpoints_IsFoundPreparedAndStillFulfils()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = PluginActivationTestSeeds.NewId("sell");
        var plugin = PluginCapabilitySeeds.Plugin(id, PluginType.Fulfilment, typeof(FulfilmentEndpointsPlugin), PluginCapabilities.Endpoints);
        await PluginActivationTestSeeds.EnableAsync(db, id);
        var activator = PluginActivationTestSeeds.Activator(factory, db);
        var resolver = new PluginEndpointResolver(activator, PluginActivationTestSeeds.Registry(plugin));

        resolver.Find(id)!.Id.Should().Be(id);
        var endpoints = await resolver.PrepareAsync(id, CancellationToken.None);
        endpoints.Should().NotBeNull();
        endpoints!().Should().BeOfType<FulfilmentEndpointsPlugin>();
        (await activator.ActivateAsync<IFulfilmentPlugin>(plugin, CancellationToken.None)).Outcome
            .Should().Be(PluginActivationOutcome.Activated);
    }

    /// <summary>The same plugin without the capability is not found and not prepared: its routes answer like an unknown id.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task FulfilmentWithoutEndpoints_IsNeitherFoundNorPrepared()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = PluginActivationTestSeeds.NewId("plain");
        await PluginActivationTestSeeds.EnableAsync(db, id);
        var resolver = new PluginEndpointResolver(
            PluginActivationTestSeeds.Activator(factory, db),
            PluginActivationTestSeeds.Registry(PluginCapabilitySeeds.Plugin(id, PluginType.Fulfilment, typeof(FulfilmentEndpointsPlugin))));

        resolver.Find(id).Should().BeNull();
        (await resolver.PrepareAsync(id, CancellationToken.None)).Should().BeNull();
    }

    /// <summary>A payment plugin declaring storedCredentials without implementing it is refused at activation too.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task PaymentDeclaringStoredCredentialsNotImplemented_IsRefused()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = PluginActivationTestSeeds.NewId("pay");
        var plugin = PluginCapabilitySeeds.Plugin(
            id, PluginType.Payment, typeof(ConfigurationCapturingPaymentPlugin), PluginCapabilities.StoredCredentials);
        await PluginActivationTestSeeds.EnableAsync(db, id);

        var activation = await PluginActivationTestSeeds.Activator(factory, db).ActivateAsync<IPaymentPlugin>(plugin, CancellationToken.None);

        activation.Outcome.Should().Be(PluginActivationOutcome.CapabilityNotImplemented);
        activation.Instance.Should().BeNull();
    }

    /// <summary>
    /// An app plugin listing a capability the host does not know (one the validator would refuse, reached here past it)
    /// fails closed at activation, and a disabled one answers Disabled before the capability is looked at.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AppWithUnknownCapability_FailsClosedAfterTheSwitch()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var on = PluginActivationTestSeeds.NewId("odd");
        var off = PluginActivationTestSeeds.NewId("odd-off");
        var activator = PluginActivationTestSeeds.Activator(factory, db);
        await PluginActivationTestSeeds.EnableAsync(db, on);

        (await activator.PrepareAsync<IPluginEndpoints>(PluginCapabilitySeeds.Plugin(on, PluginType.App, typeof(EchoAppPlugin), "teleport"), CancellationToken.None))
            .Outcome.Should().Be(PluginActivationOutcome.CapabilityNotImplemented);
        (await activator.PrepareAsync<IPluginEndpoints>(PluginCapabilitySeeds.Plugin(off, PluginType.App, typeof(EchoAppPlugin), "teleport"), CancellationToken.None))
            .Outcome.Should().Be(PluginActivationOutcome.Disabled);
    }

    /// <summary>A declared capability the class does not implement refuses the plugin for every contract, fulfilment included.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task DeclaredCapabilityNotImplemented_IsRefusedAtActivation()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = PluginActivationTestSeeds.NewId("liar");
        var plugin = PluginCapabilitySeeds.Plugin(
            id, PluginType.Fulfilment, typeof(ConfigurationCapturingFulfilmentPlugin), PluginCapabilities.Endpoints);
        await PluginActivationTestSeeds.EnableAsync(db, id);
        var activator = PluginActivationTestSeeds.Activator(factory, db);
        var resolver = new PluginEndpointResolver(activator, PluginActivationTestSeeds.Registry(plugin));

        var fulfilment = await activator.ActivateAsync<IFulfilmentPlugin>(plugin, CancellationToken.None);

        fulfilment.Outcome.Should().Be(PluginActivationOutcome.CapabilityNotImplemented);
        fulfilment.Instance.Should().BeNull();
        (await resolver.PrepareAsync(id, CancellationToken.None)).Should().BeNull();
    }
}
