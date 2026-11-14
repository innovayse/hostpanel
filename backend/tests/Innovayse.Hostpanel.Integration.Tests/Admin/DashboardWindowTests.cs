using Innovayse.Hostpanel.Application.Admin.Common;
using Innovayse.Hostpanel.Application.Admin.Common.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Admin;

/// <summary><see cref="DashboardWindow"/> lays the period and the one before it side by side, ending today in the host time zone.</summary>
public sealed class DashboardWindowTests
{
    /// <summary>The period ends on the day the host clock reads and the previous one fills the days right before it.</summary>
    /// <param name="period">The period.</param>
    [Theory]
    [InlineData(DashboardPeriod.Days30)]
    [InlineData(DashboardPeriod.Days90)]
    public void Create_EndsTodayAndPutsThePreviousPeriodRightBefore(DashboardPeriod period)
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("Host", TimeSpan.FromHours(4), "Host", "Host");
        var length = (int)period;

        var window = DashboardWindow.Create(period, new DateTimeOffset(2030, 6, 15, 22, 0, 0, TimeSpan.Zero), zone);

        window.Current.Last.Should().Be(new DateOnly(2030, 6, 16));
        window.Current.Days.Should().HaveCount(length);
        window.Previous.Days.Should().HaveCount(length);
        window.Previous.Last.Should().Be(window.Current.First.AddDays(-1));
        window.Previous.To.Should().Be(window.Current.From);
        window.Span.First.Should().Be(window.Previous.First);
        window.Span.Last.Should().Be(window.Current.Last);
        window.Span.From.Should().Be(window.Previous.From);
        window.Span.To.Should().Be(window.Current.To);
    }

    /// <summary>A value that is not one of the offered periods is refused instead of cut into some other length.</summary>
    [Fact]
    public void Create_RefusesAPeriodThatIsNotOffered()
    {
        var act = () => DashboardWindow.Create((DashboardPeriod)45, DateTimeOffset.UtcNow, TimeZoneInfo.Utc);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
