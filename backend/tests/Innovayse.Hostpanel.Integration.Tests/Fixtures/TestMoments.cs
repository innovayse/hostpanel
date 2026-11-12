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
}
