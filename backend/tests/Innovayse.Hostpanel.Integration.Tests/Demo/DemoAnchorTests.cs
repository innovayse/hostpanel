using Innovayse.Hostpanel.Infrastructure.Demo;

namespace Innovayse.Hostpanel.Integration.Tests.Demo;

/// <summary>
/// The dates the demo seed is built from, counted from the reset day alone: a customer's history is shifted to the
/// day of the reset, nothing is dated after it, a customer is never invoiced before signing up, some customers are new,
/// and how many invoices a customer has does not depend on which day of the month the reset runs on.
/// </summary>
public sealed class DemoAnchorTests
{
    /// <summary>The number of customers the demo seeds: the demo client and its filler customers.</summary>
    private const int Customers = 65;

    /// <summary>The first day the reset is simulated on.</summary>
    private static readonly DateTimeOffset FirstDay = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Days the reset is simulated over: two years, so every month end and a leap day are crossed.</summary>
    private const int SimulatedDays = 800;

    /// <summary>A reset day in the middle of a month, for the tests that do not need to cross the calendar.</summary>
    private static readonly DateTimeOffset Reset = new(2026, 11, 21, 0, 0, 0, TimeSpan.Zero);

    /// <summary>The anchor is midnight UTC of the reset day, whatever the hour of the reset.</summary>
    [Fact]
    public void Today_IsMidnightOfTheResetDay()
    {
        DemoAnchor.Today(new DateTimeOffset(2026, 11, 21, 17, 45, 12, TimeSpan.Zero)).Should().Be(Reset);
    }

    /// <summary>Every customer is invoiced at least once, never after the reset day, oldest first, and never before signing up.</summary>
    [Fact]
    public void InvoiceDates_AreOrderedNeverInTheFutureAndNeverBeforeSignup()
    {
        for (var position = 0; position < Customers; position++)
        {
            var dates = DemoAnchor.InvoiceDates(position, Reset);

            dates.Should().NotBeEmpty($"customer {position} is billed");
            dates.Should().BeInAscendingOrder().And.OnlyContain(d => d <= Reset);
            dates[0].Should().BeOnOrAfter(DemoAnchor.SignupDate(position, Reset), $"customer {position} cannot be invoiced before joining");
        }
    }

    /// <summary>The demo client keeps its full year of history, one invoice a month.</summary>
    [Fact]
    public void InvoiceDates_OfTheDemoClientCoverTwelveMonths()
    {
        DemoAnchor.InvoiceDates(0, Reset).Should().HaveCount(DemoAnchor.HistoryMonths).And.EndWith(Reset);
    }

    /// <summary>Some customers joined in the last weeks, spread over many different days, the newest within the last week.</summary>
    [Fact]
    public void SignupDate_PutsSomeCustomersInTheLastWeeksOnDifferentDays()
    {
        var recent = Enumerable.Range(0, Customers)
            .Where(DemoAnchor.IsRecentSignup)
            .Select(position => DemoAnchor.SignupDate(position, Reset))
            .ToList();

        recent.Should().OnlyContain(d => d < Reset && d >= Reset.AddDays(-90));
        recent.Count(d => d >= Reset.AddDays(-30)).Should().BeGreaterThanOrEqualTo(8);
        recent.Count(d => d >= Reset.AddDays(-7)).Should().BeGreaterThanOrEqualTo(3);
        recent.Select(d => d.Date).Distinct().Should().HaveCount(recent.Count);
        DemoAnchor.IsRecentSignup(0).Should().BeFalse("the demo client has the longest history");
    }

    /// <summary>Moving the reset day moves every date of a recent customer by the same number of days and nothing else.</summary>
    [Fact]
    public void SignupDate_OfARecentCustomerIsRelativeToTheResetDay()
    {
        var later = Reset.AddDays(40);

        foreach (var position in Enumerable.Range(0, Customers).Where(DemoAnchor.IsRecentSignup))
        {
            DemoAnchor.SignupDate(position, later).Should().Be(DemoAnchor.SignupDate(position, Reset).AddDays(40));
        }
    }

    /// <summary>
    /// How many invoices a customer has never depends on the day of the month or the month length of the reset, so the
    /// demo has the same number of rows after every reset.
    /// </summary>
    [Fact]
    public void InvoiceDates_HaveTheSameCountOnEveryResetDay()
    {
        var expected = Enumerable.Range(0, Customers).Select(p => DemoAnchor.InvoiceDates(p, FirstDay).Count).ToList();

        for (var day = 1; day < SimulatedDays; day++)
        {
            var today = FirstDay.AddDays(day);
            var counts = Enumerable.Range(0, Customers).Select(p => DemoAnchor.InvoiceDates(p, today).Count).ToList();

            counts.Should().Equal(expected, $"the invoice counts must not change on {today:yyyy-MM-dd}");
        }
    }

    /// <summary>A date is held between its bounds, and the later bound wins when they cross.</summary>
    [Fact]
    public void Clamp_HoldsAMomentBetweenItsBounds()
    {
        var earliest = Reset.AddDays(-10);
        var latest = Reset;

        DemoAnchor.Clamp(Reset.AddDays(-20), earliest, latest).Should().Be(earliest);
        DemoAnchor.Clamp(Reset.AddDays(5), earliest, latest).Should().Be(latest);
        DemoAnchor.Clamp(Reset.AddDays(-4), earliest, latest).Should().Be(Reset.AddDays(-4));
        DemoAnchor.Clamp(Reset.AddDays(-20), Reset.AddDays(3), latest).Should().Be(latest);
    }

    /// <summary>Two generators on the demo seed draw the same figures, so the seeded amounts repeat.</summary>
    [Fact]
    public void NewRandom_DrawsTheSameSequenceEveryTime()
    {
        var first = DemoAnchor.NewRandom();
        var second = DemoAnchor.NewRandom();

        Enumerable.Range(0, 20).Select(_ => first.Next()).Should().Equal(Enumerable.Range(0, 20).Select(_ => second.Next()));
    }
}
