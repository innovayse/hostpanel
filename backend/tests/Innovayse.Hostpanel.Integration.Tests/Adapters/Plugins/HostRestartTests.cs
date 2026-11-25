using Innovayse.Hostpanel.Infrastructure.Hosting;
using Innovayse.Hostpanel.Infrastructure.Hosting.Extensions;
using Innovayse.Hostpanel.Infrastructure.Plugins.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// The restart of the host: it is asked for once however many plugin changes ask, it stops the host with the restart exit
/// code, and a request that is already being handled finishes within the drain time while a new one is refused. No container or
/// database: the drain test runs a real Kestrel server on a free port.
/// </summary>
public sealed class HostRestartTests
{
    /// <summary>
    /// Several plugin changes made in a row ask for a restart each, and the host stops once: the first request starts it and the
    /// others find it pending.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Restart_SeveralChangesInARow_StopTheHostOnceAsync()
    {
        var lifetime = new CountingLifetime();
        var restarter = new HostRestarter(lifetime, Options.Create(new PluginOptions { RestartDelaySeconds = 0 }), NullLogger<HostRestarter>.Instance);

        restarter.IsRestarting.Should().BeFalse();
        restarter.Restart();
        restarter.Restart();
        restarter.Restart();
        await lifetime.Stopped.Task.WaitAsync(TimeSpan.FromSeconds(10));

        restarter.IsRestarting.Should().BeTrue("the health probe reports draining once the stop has started");
        lifetime.StopCalls.Should().Be(1);
        Environment.ExitCode.Should().Be(HostRestarter.ExitCode);
        Environment.ExitCode = 0;
    }

    /// <summary>The restart exit code is the sysexits temporary failure code, which no crash and no normal exit uses.</summary>
    [Fact]
    public void ExitCode_IsTheDedicatedRestartCode()
    {
        HostRestarter.ExitCode.Should().Be(75);
    }

    /// <summary>
    /// A request that is being handled when the restart starts finishes with its full answer inside the drain time, and a request
    /// that arrives after the stop began is refused.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Drain_ARunningRequestFinishesWithinTheDrainTimeAndANewOneIsRefusedAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddSingleton(Options.Create(new PluginOptions { DrainSeconds = 20 }));
        builder.Services.AddHostDrain();
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        await using var app = builder.Build();
        app.MapGet("/slow", async () =>
        {
            started.SetResult();
            await release.Task;
            return "finished";
        });
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        var running = client.GetStringAsync("/slow");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var stopping = app.StopAsync();
        await Task.Delay(500);

        stopping.IsCompleted.Should().BeFalse("the host waits for the running request");
        using var late = new HttpClient { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(5) };
        var refused = async () => await late.GetAsync("/slow");
        await refused.Should().ThrowAsync<HttpRequestException>("the server stopped taking new connections");

        release.SetResult();
        (await running).Should().Be("finished", "no request that was already being handled is lost");
        await stopping.WaitAsync(TimeSpan.FromSeconds(10));
    }

    /// <summary>The drain time is the host's shutdown timeout, so the host never waits longer than the option says.</summary>
    [Fact]
    public void AddHostDrain_SetsTheShutdownTimeoutToTheDrainTime()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(new PluginOptions { DrainSeconds = 42 }));
        services.AddHostDrain();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<HostOptions>>().Value.ShutdownTimeout.Should().Be(TimeSpan.FromSeconds(42));
    }

    /// <summary>A host lifetime that counts the stops it is asked for.</summary>
    private sealed class CountingLifetime : IHostApplicationLifetime
    {
        /// <summary>How many times the stop was asked for.</summary>
        private int _stopCalls;

        /// <summary>Gets the task that completes when the stop is asked for.</summary>
        public TaskCompletionSource Stopped { get; } = new();

        /// <summary>Gets how many times the stop was asked for.</summary>
        public int StopCalls => Volatile.Read(ref _stopCalls);

        /// <inheritdoc/>
        public CancellationToken ApplicationStarted => CancellationToken.None;

        /// <inheritdoc/>
        public CancellationToken ApplicationStopping => CancellationToken.None;

        /// <inheritdoc/>
        public CancellationToken ApplicationStopped => CancellationToken.None;

        /// <inheritdoc/>
        public void StopApplication()
        {
            Interlocked.Increment(ref _stopCalls);
            Stopped.TrySetResult();
        }
    }
}
