using Innovayse.Hostpanel.Application.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Common;

/// <summary><see cref="DayWindow"/> cuts calendar days in a time zone, whatever that zone does to its clock at midnight.</summary>
public sealed class DayWindowTests
{
    /// <summary>The zone the tests cut days in: three hours ahead of UTC, losing the first <paramref name="delta"/> of 10 March at midnight.</summary>
    /// <param name="delta">How far the clock jumps forward.</param>
    /// <returns>The zone, whose daylight saving ends at 01:00 daylight time on 10 October, repeating the first hour of that day.</returns>
    private static TimeZoneInfo ZoneJumpingAtMidnight(TimeSpan delta)
    {
        var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            DateTime.MinValue.Date,
            DateTime.MaxValue.Date,
            delta,
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 0, 0, 0), 3, 10),
            TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 1, 0, 0), 10, 10));
        return TimeZoneInfo.CreateCustomTimeZone("Host", TimeSpan.FromHours(3), "Host", "Host", "Host DST", [rule]);
    }

    /// <summary>The days run from the first to the last, both included, and their bounds are the zone's midnights.</summary>
    [Fact]
    public void Create_CoversTheDaysAndStartsAtTheZoneMidnight()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Host", TimeSpan.FromHours(4), "Host", "Host");

        var window = DayWindow.Create(new DateOnly(2030, 6, 14), new DateOnly(2030, 6, 16), zone);

        window.Days.Should().Equal(new DateOnly(2030, 6, 14), new DateOnly(2030, 6, 15), new DateOnly(2030, 6, 16));
        window.First.Should().Be(new DateOnly(2030, 6, 14));
        window.Last.Should().Be(new DateOnly(2030, 6, 16));
        window.From.Should().Be(new DateTimeOffset(2030, 6, 13, 20, 0, 0, TimeSpan.Zero));
        window.To.Should().Be(new DateTimeOffset(2030, 6, 16, 20, 0, 0, TimeSpan.Zero));
    }

    /// <summary>A window cannot end before it starts.</summary>
    [Fact]
    public void Create_RefusesALastDayBeforeTheFirst()
    {
        var act = () => DayWindow.Create(new DateOnly(2030, 6, 16), new DateOnly(2030, 6, 15), TimeZoneInfo.Utc);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>A moment belongs to the day the zone's clock reads, not the UTC day.</summary>
    [Fact]
    public void DayOf_ReadsTheZoneClock()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Host", new TimeSpan(5, 30, 0), "Host", "Host");
        var window = DayWindow.Create(new DateOnly(2030, 6, 15), new DateOnly(2030, 6, 16), zone);

        window.DayOf(new DateTimeOffset(2030, 6, 15, 18, 15, 0, TimeSpan.Zero)).Should().Be(new DateOnly(2030, 6, 15));
        window.DayOf(new DateTimeOffset(2030, 6, 15, 18, 45, 0, TimeSpan.Zero)).Should().Be(new DateOnly(2030, 6, 16));
    }

    /// <summary>
    /// A day whose midnight the clock jumps over starts at the jump, whatever its length: an hour, or half an hour,
    /// which a fixed one hour shift would get wrong.
    /// </summary>
    /// <param name="minutes">How long the clock jump is.</param>
    [Theory]
    [InlineData(60)]
    [InlineData(30)]
    public void Create_StartsADayAtTheClockJumpOverMidnight(int minutes)
    {
        var zone = ZoneJumpingAtMidnight(TimeSpan.FromMinutes(minutes));

        var window = DayWindow.Create(new DateOnly(2030, 3, 10), new DateOnly(2030, 3, 10), zone);

        // Midnight on the zone's standard offset of +03:00 is 21:00 UTC the evening before, and that is when the jump happens.
        window.From.Should().Be(new DateTimeOffset(2030, 3, 9, 21, 0, 0, TimeSpan.Zero));
        window.DayOf(window.From).Should().Be(new DateOnly(2030, 3, 10));
        window.DayOf(window.From.AddTicks(-1)).Should().Be(new DateOnly(2030, 3, 9));
    }

    /// <summary>A day whose midnight happens twice starts the first time, on the daylight offset.</summary>
    [Fact]
    public void Create_StartsADayAtTheFirstOfTwoMidnights()
    {
        var zone = ZoneJumpingAtMidnight(TimeSpan.FromHours(1));

        var window = DayWindow.Create(new DateOnly(2030, 10, 10), new DateOnly(2030, 10, 10), zone);

        // Daylight time is +04:00, so the first midnight is 20:00 UTC the evening before.
        window.From.Should().Be(new DateTimeOffset(2030, 10, 9, 20, 0, 0, TimeSpan.Zero));
    }
}
