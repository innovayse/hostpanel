using System.Runtime.CompilerServices;
using Innovayse.Hostpanel.Application.Billing.Commands.DispatchFulfilmentNow;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.Runtime;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>
/// Proves that disposed test hosts are released (issue 793): a disposed <see cref="IntegrationTestFactory"/>
/// and the Wolverine runtime of its host must be collectable. Without the early pool (see
/// <c>IntegrationTestFactory.InitializeAsync</c>) the Npgsql pool timer, created inside the host's execution
/// context, keeps every stopped host reachable from a static table (verified: the test fails when the pool is
/// opened inside the host context). It does not pin <see cref="WolverineListenerRelease"/>: that test stays green
/// without it, while the shard run runs out of memory (see the remarks of that type).
/// </summary>
public sealed class DisposedHostReleaseTests
{
    /// <summary>How many factories the test tracks; one alone could be kept alive by a stack slot, three show a leak per host.</summary>
    private const int FactoryCount = 3;

    /// <summary>
    /// Proves that disposed hosts are released. A host is started and disposed first and not tracked, then three factories are started, used and disposed;
    /// after two full collections with finalizers neither a factory nor the Wolverine runtime of its host is still
    /// alive. The first host is left out on purpose: EF Core keeps the options of the first <c>AppDbContext</c> of
    /// the process, whose interceptors are scoped services of that host, in a static service provider cache, so the
    /// first host of the process stays reachable whatever is released (found with <c>gcroot</c> on a dump:
    /// <c>ServiceProviderCache</c> to the options to <c>FulfilmentRecordInterceptor</c> to the host's scope). It is
    /// one host per process, not one per factory, and later hosts reuse that entry; the leak this test pins grows
    /// with every host.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task DisposedFactories_AfterCollection_ReleaseTheirHostsAndRuntimesAsync()
    {
        await StartUseAndDisposeAsync();
        var (factories, runtimes) = await StartUseAndDisposeAllAsync();

        // The last factory's disposal can still be on this thread's stack: its continuation runs inline, inside
        // the disposal's own frames, which keep the factory reachable. Yielding resumes on a fresh stack.
        await Task.Yield();

        // Twice, because a finalizer can release the last reference to something the first pass found alive.
        for (var pass = 0; pass < 2; pass++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        AliveIndexes(factories).Should().BeEmpty("a disposed factory must not stay reachable");
        AliveIndexes(runtimes).Should().BeEmpty("a disposed host's Wolverine runtime must not stay reachable");
    }

    /// <summary>The positions of the references whose target is still alive; the position says which host leaked.</summary>
    /// <param name="references">The weak references to inspect.</param>
    /// <returns>The positions of the live references.</returns>
    private static int[] AliveIndexes(WeakReference[] references)
    {
        return Enumerable.Range(0, references.Length).Where(index => references[index].IsAlive).ToArray();
    }

    /// <summary>
    /// Starts, uses and disposes <see cref="FactoryCount"/> factories and returns only weak references to them.
    /// Kept out of line so that no local of the test method (or of an inlined callee) keeps a factory alive
    /// across the collection.
    /// </summary>
    /// <returns>Weak references to the factories and to the Wolverine runtimes of their hosts.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<(WeakReference[] Factories, WeakReference[] Runtimes)> StartUseAndDisposeAllAsync()
    {
        var factories = new WeakReference[FactoryCount];
        var runtimes = new WeakReference[FactoryCount];

        for (var index = 0; index < FactoryCount; index++)
        {
            (factories[index], runtimes[index]) = await StartUseAndDisposeAsync();
        }

        return (factories, runtimes);
    }

    /// <summary>Starts a factory, makes its host do real work, disposes it and returns weak references to it and to its runtime.</summary>
    /// <returns>Weak references to the factory and to the Wolverine runtime of its host.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<(WeakReference Factory, WeakReference Runtime)> StartUseAndDisposeAsync()
    {
        var factory = new IntegrationTestFactory();
        await factory.InitializeAsync();

        // A request makes the host do real work (handlers compiled, listeners busy) before it is released.
        await factory.GetAdminTokenAsync();
        using (var client = factory.CreateClient())
        {
            (await client.GetAsync("/health")).Dispose();
        }

        // A published message goes through the local queue's listening agent, which is what starts the agent's
        // work (and its timers) from the context of the caller; a host that only served requests never used it.
        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(new DispatchFulfilmentNowCommand());
        }

        var references = (new WeakReference(factory), new WeakReference(factory.Services.GetRequiredService<IWolverineRuntime>()));
        await factory.DisposeAsync();
        return references;
    }
}
