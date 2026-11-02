using System.Collections.Concurrent;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// The handler of <see cref="SplitProbeEvent"/> that throws a transient fault on its first attempt and
/// succeeds on every later one. A host adds it, and its sibling <see cref="SplitProbeSteadyHandler"/>, with
/// <see cref="IncludeIn"/>; the API's own discovery never sees the test assembly.
/// </summary>
/// <remarks>
/// The counts are static because Wolverine builds a handler per message, so an instance field would not
/// survive to the retry. Keyed by probe id, so tests in parallel never read each other's.
/// </remarks>
public sealed class SplitProbeFailingHandler
{
    /// <summary>How many times each probe reached this handler, by probe id.</summary>
    private static readonly ConcurrentDictionary<Guid, int> Attempts = new();

    /// <summary>The probes this handler finished without throwing.</summary>
    private static readonly ConcurrentDictionary<Guid, bool> Succeeded = new();

    /// <summary>Adds both handlers of <see cref="SplitProbeEvent"/> to a host's Wolverine discovery.</summary>
    /// <param name="options">The Wolverine options of the host under test.</param>
    public static void IncludeIn(WolverineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Discovery.IncludeType<SplitProbeFailingHandler>();
        options.Discovery.IncludeType<SplitProbeSteadyHandler>();
    }

    /// <summary>How many times the probe reached this handler so far.</summary>
    /// <param name="probeId">The probe.</param>
    /// <returns>The number of attempts, 0 when it has not arrived.</returns>
    public static int AttemptsFor(Guid probeId) => Attempts.GetValueOrDefault(probeId);

    /// <summary>Whether an attempt of this handler for the probe finished without throwing.</summary>
    /// <param name="probeId">The probe.</param>
    /// <returns><see langword="true"/> once the probe was handled successfully here.</returns>
    public static bool HasSucceeded(Guid probeId) => Succeeded.ContainsKey(probeId);

    /// <summary>Counts the attempt, fails the first one and records success on any later one.</summary>
    /// <param name="message">The probe being delivered.</param>
    /// <exception cref="InvalidOperationException">On the first attempt for a probe, as a transient fault.</exception>
    public void Handle(SplitProbeEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var attempt = Attempts.AddOrUpdate(message.ProbeId, 1, (_, previous) => previous + 1);
        if (attempt == 1)
        {
            throw new InvalidOperationException("Transient fault on the first attempt of a split probe.");
        }

        Succeeded[message.ProbeId] = true;
    }
}
