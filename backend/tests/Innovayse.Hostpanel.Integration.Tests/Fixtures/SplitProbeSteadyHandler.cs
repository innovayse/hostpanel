using System.Collections.Concurrent;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// The handler of <see cref="SplitProbeEvent"/> that always succeeds and counts how often it ran, so a test
/// can see whether a retry of its failing sibling (<see cref="SplitProbeFailingHandler"/>) ran it again.
/// Added to a host by <see cref="SplitProbeFailingHandler.IncludeIn"/>.
/// </summary>
/// <remarks>Static and keyed by probe id for the same reasons as its sibling's counts.</remarks>
public sealed class SplitProbeSteadyHandler
{
    /// <summary>How many times each probe reached this handler, by probe id.</summary>
    private static readonly ConcurrentDictionary<Guid, int> Runs = new();

    /// <summary>How many times the probe reached this handler so far.</summary>
    /// <param name="probeId">The probe.</param>
    /// <returns>The number of runs, 0 when it has not arrived.</returns>
    public static int RunsFor(Guid probeId) => Runs.GetValueOrDefault(probeId);

    /// <summary>Counts the run.</summary>
    /// <param name="message">The probe being delivered.</param>
    public void Handle(SplitProbeEvent message)
    {
        ArgumentNullException.ThrowIfNull(message);

        Runs.AddOrUpdate(message.ProbeId, 1, (_, previous) => previous + 1);
    }
}
