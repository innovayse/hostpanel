using Innovayse.Hostpanel.Application.Common.Interfaces;

namespace Innovayse.Hostpanel.Infrastructure.Time;

/// <summary>
/// The <see cref="IClock"/> backed by the registered <see cref="TimeProvider"/>.
/// </summary>
/// <remarks>
/// It reads the same <see cref="TimeProvider"/> the token and two-factor services already use, so
/// replacing that one registration in a test moves every clock in the process together.
/// </remarks>
public sealed class SystemClock : IClock
{
    /// <summary>The time source every reading comes from.</summary>
    private readonly TimeProvider _time;

    /// <summary>Initializes a new instance of the <see cref="SystemClock"/> class.</summary>
    /// <param name="time">The time source every reading comes from.</param>
    public SystemClock(TimeProvider time)
    {
        _time = time;
    }

    /// <inheritdoc/>
    public DateTimeOffset UtcNow => _time.GetUtcNow();

    /// <inheritdoc/>
    public TimeZoneInfo TimeZone => _time.LocalTimeZone;
}
