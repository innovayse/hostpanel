using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Infrastructure.Plugins;

/*
 * Why the panels' plugin availability is remembered for a few seconds, for every plugin at once.
 *
 * Deciding whether a plugin is available reads its switch and its required settings from the database
 * (PluginActivator.PrepareAsync). Asked per plugin and per request, the index read the database once for every loaded
 * plugin on every page load, and the page lookup answered an unknown id without any read but an installed one after a
 * read: the time to answer told a caller which plugin ids exist, and every index request cost one query per plugin.
 *
 * So one pass computes the set of available app plugins for all of them together, and both the index and the page
 * lookup answer from that same set: an unknown id and an installed one take the same path, a lookup in memory. The set
 * lives for Lifetime (short, so a change made outside the admin use cases, such as a row edited by hand, still shows
 * within seconds) and is dropped at once by IPluginAvailabilityInvalidator after every use case that writes a plugin's
 * settings. A generation counter keeps a pass that was running while an invalidation happened from storing its now
 * stale answer, and one pass runs at a time, so an expiry under load costs one pass, not one per waiting request.
 */

/// <summary>
/// Remembers, for <see cref="Lifetime"/>, which loaded app plugins are available to the panels, computed for every
/// plugin in one pass. A singleton: the remembered set is shared by every request of the process.
/// </summary>
public sealed class PluginAvailabilityCache : IDisposable
{
    /// <summary>How long a computed set is answered from memory, in seconds.</summary>
    public const int LifetimeSeconds = 10;

    /// <summary>How long a computed set is answered from memory.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(LifetimeSeconds);

    /// <summary>The moment a set is computed at, and against which its expiry is judged.</summary>
    private readonly IClock _clock;

    /// <summary>Lets one pass compute the set at a time, so concurrent requests after an expiry share it.</summary>
    private readonly SemaphoreSlim _computing = new(1, 1);

    /// <summary>Guards <see cref="_current"/> and <see cref="_generation"/>, which an invalidation changes together.</summary>
    private readonly Lock _gate = new();

    /// <summary>The remembered set, or <see langword="null"/> before the first pass and after an invalidation.</summary>
    private PluginAvailabilitySnapshot? _current;

    /// <summary>Raised by every invalidation, so a pass that started before one does not store its answer.</summary>
    private long _generation;

    /// <summary>Initializes a new instance of the <see cref="PluginAvailabilityCache"/> class.</summary>
    /// <param name="clock">The moment a set is computed at, and against which its expiry is judged.</param>
    public PluginAvailabilityCache(IClock clock)
    {
        _clock = clock;
    }

    /// <summary>
    /// Answers the remembered set while it is fresh, and otherwise computes it once, remembers it and answers it.
    /// </summary>
    /// <param name="compute">Runs the activation gates of every loaded app plugin and returns the ids that pass.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ids of the available app plugins, compared ordinally.</returns>
    public async Task<IReadOnlySet<string>> GetOrComputeAsync(
        Func<CancellationToken, Task<IReadOnlySet<string>>> compute,
        CancellationToken ct)
    {
        var fresh = Fresh();
        if (fresh is not null)
        {
            return fresh;
        }

        await _computing.WaitAsync(ct);
        try
        {
            // Asked again under the semaphore: a request that waited here finds the set the pass before it stored.
            fresh = Fresh();
            if (fresh is not null)
            {
                return fresh;
            }

            long generation;
            lock (_gate)
            {
                generation = _generation;
            }

            var computed = await compute(ct);
            lock (_gate)
            {
                // An invalidation during the pass means the switches may have changed after they were read; the
                // answer is still right for this request, but the next one must read them again.
                if (generation == _generation)
                {
                    _current = new PluginAvailabilitySnapshot(computed, _clock.UtcNow + Lifetime);
                }
            }

            return computed;
        }
        finally
        {
            _computing.Release();
        }
    }

    /// <summary>Forgets the remembered set, so the next lookup computes it again.</summary>
    public void Invalidate()
    {
        lock (_gate)
        {
            _generation++;
            _current = null;
        }
    }

    /// <summary>Releases the semaphore that serialises the passes.</summary>
    public void Dispose()
    {
        _computing.Dispose();
    }

    /// <summary>The remembered set while it has not expired.</summary>
    /// <returns>The set, or <see langword="null"/> when there is none or it has expired.</returns>
    private IReadOnlySet<string>? Fresh()
    {
        lock (_gate)
        {
            return _current is not null && _clock.UtcNow < _current.ExpiresAt ? _current.PluginIds : null;
        }
    }
}
