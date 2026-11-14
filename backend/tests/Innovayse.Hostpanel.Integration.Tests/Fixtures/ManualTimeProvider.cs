namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A clock a test moves by hand, so token lifetimes can be crossed without waiting.
/// </summary>
public sealed class ManualTimeProvider : TimeProvider
{
    /// <summary>The current instant this clock reports.</summary>
    private DateTimeOffset _now;

    /// <summary>The time zone the clock reports as the host's, or null for the real local one.</summary>
    private readonly TimeZoneInfo? _zone;

    /// <summary>Initializes a new instance of the <see cref="ManualTimeProvider"/> class.</summary>
    /// <param name="start">The instant the clock starts at.</param>
    /// <param name="zone">The time zone the clock reports as the host's; the real local one when omitted.</param>
    public ManualTimeProvider(DateTimeOffset start, TimeZoneInfo? zone = null)
    {
        _now = start;
        _zone = zone;
    }

    /// <inheritdoc/>
    public override TimeZoneInfo LocalTimeZone => _zone ?? base.LocalTimeZone;

    /// <summary>Moves the clock forward (or back, for a negative span).</summary>
    /// <param name="by">How far to move it.</param>
    public void Advance(TimeSpan by)
    {
        _now += by;
    }

    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow()
    {
        return _now;
    }
}
