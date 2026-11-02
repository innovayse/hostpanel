using Microsoft.Extensions.Hosting;
using Wolverine.Runtime;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Disposes the Wolverine listening agents of a test host when its container is disposed, so each listener
/// (the local queues included) and the back pressure timer it owns are released with the host and not left to
/// the runtime's own disposal.
/// </summary>
/// <remarks>
/// Measured on the heavy integration shard (issue 793), shard 3 of the CI matrix: with this release it completes
/// inside the 6 GiB cap, without it the test process reaches about 5.6 GB, throws <see cref="OutOfMemoryException"/>
/// and 178 tests fail. A test that tracks three disposed hosts with weak references stays green without it, so
/// that test does not prove this class is needed; the shard run does. The cause of that difference is not
/// established here, and the type makes no claim about what keeps a stopped host reachable.
/// </remarks>
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
