namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// The moments tests use, so a test does not depend on when it happens to run: one fixed moment for everything that runs on a
/// clocked host, and the real present for the few things that must agree with the shared host's own clock.
/// </summary>
internal static class TestMoments
{
    /// <summary>
    /// A fixed moment used to label seeded rows and to start manual clocks. It is a whole second, so it
    /// survives a round trip through a database column unchanged.
    /// </summary>
    public static DateTimeOffset Seeded { get; } = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Gets the real present, the one place the tests read the system clock. It is for what has to agree with the shared host,
    /// which runs on the system clock: a row the shared host will judge by its own date (a due date, an expiry, a lease), the
    /// bound a stamp the shared host wrote is checked against, and the start of a derived host that must accept the tokens and
    /// cookies the shared host mints (they carry real expiry). A test about a time window runs on a clocked host at
    /// <see cref="Seeded"/> instead. The moment is cut to a whole second, so it survives a round trip through a database
    /// column unchanged and a stored copy of it compares with <c>Be</c>; a stamp the shared host wrote itself keeps the full
    /// precision of the system clock, so a test compares that one with <c>BeCloseTo</c>.
    /// </summary>
    /// <returns>The system's current moment, without its fraction of a second.</returns>
    public static DateTimeOffset Present()
    {
        var now = TimeProvider.System.GetUtcNow();
        return new DateTimeOffset(now.Ticks - (now.Ticks % TimeSpan.TicksPerSecond), TimeSpan.Zero);
    }
}
