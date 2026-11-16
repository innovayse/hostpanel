namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Runs a block of a test with the host time zone set to a zone other than UTC, the way a deployment on a local zone runs,
/// and puts the machine's own zone back when it ends. Only a test of <see cref="ProcessTimeZoneCollection"/> may use it.
/// </summary>
public sealed class ProcessTimeZone : IDisposable
{
    /// <summary>The environment variable the runtime reads its local zone from.</summary>
    private const string ZoneVariable = "TZ";

    /// <summary>The value the zone variable had before, restored on dispose; null when it was not set.</summary>
    private readonly string? _previous;

    /// <summary>Initializes a new instance of the <see cref="ProcessTimeZone"/> class and makes the zone the local one.</summary>
    /// <param name="zoneId">The IANA id of the zone, for example <c>Asia/Yerevan</c>.</param>
    public ProcessTimeZone(string zoneId)
    {
        _previous = Environment.GetEnvironmentVariable(ZoneVariable);
        Environment.SetEnvironmentVariable(ZoneVariable, zoneId);
        TimeZoneInfo.ClearCachedData();

        // A zone the machine has no data for silently falls back to UTC, and a test that ran on UTC would prove nothing.
        TimeZoneInfo.Local.GetUtcOffset(DateTimeOffset.UnixEpoch).Should().NotBe(TimeSpan.Zero, $"{zoneId} must be the local zone");
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Environment.SetEnvironmentVariable(ZoneVariable, _previous);
        TimeZoneInfo.ClearCachedData();
    }
}
