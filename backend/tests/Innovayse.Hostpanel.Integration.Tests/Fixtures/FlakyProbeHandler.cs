using System.Collections.Concurrent;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Test-only handler of <see cref="FlakyProbeMessage"/>: the first attempt for a probe throws a transient
/// fault and every later attempt succeeds. It returns no <c>Result</c> and is not a recurring job, so its
/// chain is a queued delivery and gets the durable retry rules. A host adds it with
/// <see cref="IncludeIn"/>; the API's own discovery never sees the test assembly.
/// </summary>
/// <remarks>
/// The attempt counts are static because Wolverine builds a handler per message, so an instance field
/// would not survive to the retry. Keyed by probe id, so tests in parallel never read each other's.
/// </remarks>
public sealed class FlakyProbeHandler
{
    /// <summary>How many times each probe reached the handler, by probe id.</summary>
    private static readonly ConcurrentDictionary<Guid, int> Attempts = new();

    /// <summary>The probes whose handling finished without throwing.</summary>
    private static readonly ConcurrentDictionary<Guid, bool> Succeeded = new();

    /// <summary>Adds this handler to a host's Wolverine discovery.</summary>
    /// <param name="options">The Wolverine options of the host under test.</param>
    public static void IncludeIn(WolverineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Discovery.IncludeType<FlakyProbeHandler>();
    }

    /// <summary>How many times the probe reached the handler so far.</summary>
    /// <param name="probeId">The probe.</param>
    /// <returns>The number of attempts, 0 when it has not arrived.</returns>
    public static int AttemptsFor(Guid probeId) => Attempts.GetValueOrDefault(probeId);

    /// <summary>Whether an attempt for the probe finished without throwing.</summary>
    /// <param name="probeId">The probe.</param>
    /// <returns><see langword="true"/> once the probe was handled successfully.</returns>
    public static bool HasSucceeded(Guid probeId) => Succeeded.ContainsKey(probeId);

    /// <summary>Counts the attempt, fails the first one and records success on any later one.</summary>
    /// <param name="message">The probe being delivered.</param>
    /// <exception cref="InvalidOperationException">On the first attempt for a probe, as a transient fault.</exception>
    public void Handle(FlakyProbeMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        var attempt = Attempts.AddOrUpdate(message.ProbeId, 1, (_, previous) => previous + 1);
        if (attempt == 1)
        {
            throw new InvalidOperationException("Transient fault on the first attempt of a flaky probe.");
        }

        Succeeded[message.ProbeId] = true;
    }
}
