using Innovayse.Hostpanel.Application.Admin.Common.Enums;
using Innovayse.Hostpanel.Application.Common;

namespace Innovayse.Hostpanel.Application.Admin.Common;

/// <summary>
/// The days a dashboard period covers and the period of the same length right before it, both cut in the host
/// time zone. Every series of the dashboard is bucketed by these days, so charts agree on where a day ends.
/// </summary>
public sealed class DashboardWindow
{
    /// <summary>Initializes the window.</summary>
    /// <param name="current">The days of the period, today last.</param>
    /// <param name="previous">The days of the period of the same length right before it.</param>
    /// <param name="span">Both periods as one run of days.</param>
    private DashboardWindow(DayWindow current, DayWindow previous, DayWindow span)
    {
        Current = current;
        Previous = previous;
        Span = span;
    }

    /// <summary>Gets the days of the period, oldest first, today last.</summary>
    public DayWindow Current { get; }

    /// <summary>Gets the days of the period of the same length right before <see cref="Current"/>.</summary>
    public DayWindow Previous { get; }

    /// <summary>Gets the previous period and the current one as one run of days, for a read that serves both.</summary>
    public DayWindow Span { get; }

    /// <summary>Builds the window that ends with the day <paramref name="now"/> falls in.</summary>
    /// <param name="period">How many days the window covers.</param>
    /// <param name="now">The current moment.</param>
    /// <param name="zone">The time zone days are cut by.</param>
    /// <returns>The window.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="period"/> is not one of the offered periods.</exception>
    public static DashboardWindow Create(DashboardPeriod period, DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        if (!Enum.IsDefined(period))
        {
            throw new ArgumentOutOfRangeException(nameof(period), period, "The dashboard offers only the periods of DashboardPeriod.");
        }

        var length = (int)period;
        var today = DayWindow.DayIn(now, zone);
        var current = DayWindow.Create(today.AddDays(1 - length), today, zone);
        var previous = DayWindow.Create(current.First.AddDays(-length), current.First.AddDays(-1), zone);
        return new DashboardWindow(current, previous, DayWindow.Create(previous.First, current.Last, zone));
    }
}
