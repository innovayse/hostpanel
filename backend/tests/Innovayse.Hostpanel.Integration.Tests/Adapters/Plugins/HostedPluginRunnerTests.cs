using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.SDK.Plugins.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// <see cref="HostedPluginRunner"/>: enabled hosted plugins are started and stopped, and a plugin whose start or
/// stop throws is logged and left out while the runner and every other plugin carry on, against Postgres.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class HostedPluginRunnerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// A plugin whose start throws neither stops the runner nor keeps the other plugin from starting and
    /// stopping, and it is never asked to stop itself.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StartFailure_IsIsolatedToThatPlugin()
    {
        var journal = new HostedPluginJournal();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var failing = PluginActivationTestSeeds.NewId("a-hosted");
        var healthy = PluginActivationTestSeeds.NewId("b-hosted");
        await PluginActivationTestSeeds.EnableAsync(db, failing, healthy);
        await using var services = Container(db, journal);
        var runner = Runner(
            services,
            PluginActivationTestSeeds.Plugin(failing, PluginType.Hosted, typeof(StartFailingHostedPlugin)),
            PluginActivationTestSeeds.Plugin(healthy, PluginType.Hosted, typeof(RecordingHostedPlugin)));

        var start = () => runner.StartedAsync(CancellationToken.None);
        await start.Should().NotThrowAsync();
        await runner.StoppingAsync(CancellationToken.None);

        journal.Started.Should().Equal(typeof(RecordingHostedPlugin));
        journal.Stopped.Should().Equal(typeof(RecordingHostedPlugin));
    }

    /// <summary>A plugin whose stop throws does not stop the runner or keep the other plugin from stopping.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StopFailure_IsIsolatedToThatPlugin()
    {
        var journal = new HostedPluginJournal();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var failing = PluginActivationTestSeeds.NewId("a-hosted");
        var healthy = PluginActivationTestSeeds.NewId("b-hosted");
        await PluginActivationTestSeeds.EnableAsync(db, failing, healthy);
        await using var services = Container(db, journal);
        var runner = Runner(
            services,
            PluginActivationTestSeeds.Plugin(failing, PluginType.Hosted, typeof(StopFailingHostedPlugin)),
            PluginActivationTestSeeds.Plugin(healthy, PluginType.Hosted, typeof(RecordingHostedPlugin)));
        await runner.StartedAsync(CancellationToken.None);

        var stop = () => runner.StoppingAsync(CancellationToken.None);

        await stop.Should().NotThrowAsync();
        journal.Started.Should().BeEquivalentTo(new[] { typeof(StopFailingHostedPlugin), typeof(RecordingHostedPlugin) });
        journal.Stopped.Should().BeEquivalentTo(new[] { typeof(StopFailingHostedPlugin), typeof(RecordingHostedPlugin) });
    }

    /// <summary>A hosted plugin the admin has not switched on is never started, and so never stopped.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DisabledPlugin_IsNotStarted()
    {
        var journal = new HostedPluginJournal();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var services = Container(db, journal);
        var runner = Runner(
            services,
            PluginActivationTestSeeds.Plugin(PluginActivationTestSeeds.NewId("hosted"), PluginType.Hosted, typeof(RecordingHostedPlugin)));

        await runner.StartedAsync(CancellationToken.None);
        await runner.StoppingAsync(CancellationToken.None);

        journal.Started.Should().BeEmpty();
        journal.Stopped.Should().BeEmpty();
    }

    /// <summary>
    /// A plugin of another type that also implements the hosted contract is started too, as the SDK allows a
    /// hosted worker beside another contract.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PluginOfAnotherTypeImplementingTheHostedContract_IsStarted()
    {
        var journal = new HostedPluginJournal();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var id = PluginActivationTestSeeds.NewId("fulfil");
        await PluginActivationTestSeeds.EnableAsync(db, id);
        await using var services = Container(db, journal);
        var runner = Runner(services, PluginActivationTestSeeds.Plugin(id, PluginType.Fulfilment, typeof(RecordingHostedPlugin)));

        await runner.StartedAsync(CancellationToken.None);
        await runner.StoppingAsync(CancellationToken.None);

        journal.Started.Should().Equal(typeof(RecordingHostedPlugin));
        journal.Stopped.Should().Equal(typeof(RecordingHostedPlugin));
    }

    /// <summary>
    /// The container the runner activates plugins from: the real activator over the test's own data context,
    /// and the journal the test plugins take in their constructor.
    /// </summary>
    /// <param name="db">The data context the activator reads settings from; the test keeps it alive.</param>
    /// <param name="journal">The journal the test plugins record into.</param>
    /// <returns>The container; the test disposes it.</returns>
    private ServiceProvider Container(AppDbContext db, HostedPluginJournal journal)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddSingleton(factory.Services.GetRequiredService<ISecretSettingCipher>());
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddSingleton(journal);
        services.AddScoped<PluginActivator>();
        return services.BuildServiceProvider();
    }

    /// <summary>Builds the runner over a registry holding <paramref name="plugins"/>.</summary>
    /// <param name="services">The container the runner opens its scope in.</param>
    /// <param name="plugins">The plugins the registry reports as loaded.</param>
    /// <returns>The runner.</returns>
    private static HostedPluginRunner Runner(ServiceProvider services, params LoadedPlugin[] plugins)
    {
        return new HostedPluginRunner(
            PluginActivationTestSeeds.Registry(plugins),
            services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<HostedPluginRunner>.Instance);
    }
}
