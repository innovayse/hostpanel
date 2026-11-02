using Microsoft.Extensions.Hosting;
using Wolverine.Runtime;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Disposes the Wolverine listening agents of a test host when its container is disposed. Every listening agent
/// (the local queues included) starts a back pressure timer, a <c>System.Timers.Timer</c> that nothing stops
/// when the host does; the timer queue is static, so each stopped host stayed reachable through its timers,
/// firing every few seconds for the rest of the run, with its container, its Wolverine runtime and everything
/// they hold: 38 runtimes alive after 70 hosts, and a process killed at 6 GiB before the shard finished
/// (issue 793). Disposing the agent disposes its timer.
/// </summary>
internal sealed class WolverineListenerRelease : IHostedService, IAsyncDisposable
{
    /// <summary>The runtime whose listening agents are released.</summary>
    private readonly IWolverineRuntime _runtime;

    /// <summary>Initializes a new instance of the <see cref="WolverineListenerRelease"/> class.</summary>
    /// <param name="runtime">The runtime whose listening agents are released.</param>
    public WolverineListenerRelease(IWolverineRuntime runtime)
    {
        _runtime = runtime;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>Disposes every listening agent the runtime started.</summary>
    /// <returns>A task that completes when the agents are disposed.</returns>
    public async ValueTask DisposeAsync()
    {
        foreach (var agent in _runtime.Endpoints.ActiveListeners().OfType<IAsyncDisposable>().ToList())
        {
            await agent.DisposeAsync();
        }
    }
}
