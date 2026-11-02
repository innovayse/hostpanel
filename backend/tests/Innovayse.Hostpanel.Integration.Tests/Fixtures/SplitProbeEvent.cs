namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A test-only event with two handlers: <see cref="SplitProbeFailingHandler"/> throws on its first attempt,
/// <see cref="SplitProbeSteadyHandler"/> always succeeds. It exists so a test can prove that a retry of one
/// handler does not run its sibling again.
/// </summary>
/// <param name="ProbeId">Identifies one probe, so tests running beside each other count only their own attempts.</param>
public sealed record SplitProbeEvent(Guid ProbeId);
