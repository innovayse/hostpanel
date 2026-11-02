namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A test-only message delivered through a durable local queue, whose handler
/// (<see cref="FlakyProbeHandler"/>) throws on its first attempt and succeeds on the next. It exists so a
/// test can prove that a queued handler that fails is retried rather than lost.
/// </summary>
/// <param name="ProbeId">Identifies one probe, so tests running beside each other count only their own attempts.</param>
public sealed record FlakyProbeMessage(Guid ProbeId);
