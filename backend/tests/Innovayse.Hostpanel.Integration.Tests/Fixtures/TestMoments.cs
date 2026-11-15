namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Fixed moments for tests, so a test does not depend on the real clock.
/// </summary>
internal static class TestMoments
{
    /// <summary>
    /// A fixed moment used to label seeded rows and to start manual clocks. It is a whole second, so it
    /// survives a round trip through a database column unchanged.
    /// </summary>
    public static DateTimeOffset Seeded { get; } = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Gets the real present, the one place the tests read the system clock. It is real time on purpose: a recurring job
    /// schedules its next run from its host's clock, and a clock standing in the past makes that run due at once, so the job
    /// would repeat on the host and its passes would meet the lease of their own repeats; a derived host that must accept
    /// tokens minted by the shared host (which runs on the system clock) needs the same moment, never a moment behind it.
    /// It keeps the clock's full precision, so a test compares a stored moment with it through <c>BeCloseTo</c>, not <c>Be</c>.
    /// </summary>
    /// <returns>The system's current moment.</returns>
    public static DateTimeOffset Present()
    {
        return TimeProvider.System.GetUtcNow();
    }
}
