using Innovayse.Hostpanel.Shared.Infrastructure;

namespace Innovayse.Hostpanel.Integration.Tests.Shared;

/// <summary>Pins <see cref="JobSchedule"/>: the next run and the "missed its turn" question, for both kinds of job.</summary>
public sealed class JobScheduleTests
{
    /// <summary>A moment half an hour after the 05:00 hour of the daily jobs below.</summary>
    private static readonly DateTimeOffset AfterTheHour = new(2026, 3, 10, 5, 30, 0, TimeSpan.Zero);

    /// <summary>A daily job's next run is its next occurrence of the hour.</summary>
    [Fact]
    public void Daily_NextAfter_IsTheNextOccurrenceOfTheHour()
    {
        JobSchedule.Daily(5).NextAfter(AfterTheHour).Should().Be(new DateTimeOffset(2026, 3, 11, 5, 0, 0, TimeSpan.Zero));
        JobSchedule.Daily(7).NextAfter(AfterTheHour).Should().Be(new DateTimeOffset(2026, 3, 10, 7, 0, 0, TimeSpan.Zero));
    }

    /// <summary>An interval job's next run is the interval after the moment it is asked from, so a long run never overlaps the next.</summary>
    [Fact]
    public void Every_NextAfter_IsTheIntervalAfterNow()
    {
        JobSchedule.Every(TimeSpan.FromMinutes(5)).NextAfter(AfterTheHour).Should().Be(AfterTheHour.AddMinutes(5));
    }

    /// <summary>A job that has never succeeded has missed its turn.</summary>
    [Fact]
    public void IsOverdue_WhenNeverSucceeded_IsTrue()
    {
        JobSchedule.Daily(5).IsOverdue(null, AfterTheHour).Should().BeTrue();
        JobSchedule.Every(TimeSpan.FromMinutes(5)).IsOverdue(null, AfterTheHour).Should().BeTrue();
    }

    /// <summary>A restart during the run hour: the day is missed when the last success is from before today's hour.</summary>
    [Fact]
    public void Daily_IsOverdue_WhenTheLastSuccessIsBeforeTodaysHour()
    {
        var yesterday = AfterTheHour.AddDays(-1);

        JobSchedule.Daily(5).IsOverdue(yesterday, AfterTheHour).Should().BeTrue();
    }

    /// <summary>A day whose run already succeeded is not caught up again.</summary>
    [Fact]
    public void Daily_IsNotOverdue_WhenTodaysRunSucceeded()
    {
        var today = new DateTimeOffset(2026, 3, 10, 5, 10, 0, TimeSpan.Zero);

        JobSchedule.Daily(5).IsOverdue(today, AfterTheHour).Should().BeFalse();
    }

    /// <summary>Before the hour, yesterday's success is current: nothing is due yet.</summary>
    [Fact]
    public void Daily_IsNotOverdue_BeforeTodaysHourWhenYesterdaySucceeded()
    {
        var earlyMorning = new DateTimeOffset(2026, 3, 10, 3, 0, 0, TimeSpan.Zero);
        var yesterdaysRun = new DateTimeOffset(2026, 3, 9, 5, 10, 0, TimeSpan.Zero);

        JobSchedule.Daily(5).IsOverdue(yesterdaysRun, earlyMorning).Should().BeFalse();
    }

    /// <summary>An interval job is overdue once a full interval has passed since its last success.</summary>
    [Fact]
    public void Every_IsOverdue_OnlyAfterAFullInterval()
    {
        var schedule = JobSchedule.Every(TimeSpan.FromMinutes(15));

        schedule.IsOverdue(AfterTheHour.AddMinutes(-14), AfterTheHour).Should().BeFalse();
        schedule.IsOverdue(AfterTheHour.AddMinutes(-15), AfterTheHour).Should().BeTrue();
    }

    /// <summary>A daily job keeps the configured rest.</summary>
    [Fact]
    public void Daily_RestAfterSuccess_IsTheConfiguredRest()
    {
        JobSchedule.Daily(5).RestAfterSuccess(TimeSpan.FromSeconds(60)).Should().Be(TimeSpan.FromSeconds(60));
    }

    /// <summary>An interval job never rests longer than half its interval, so its own next beat is never closed.</summary>
    /// <param name="intervalSeconds">The interval of the job.</param>
    /// <param name="expectedSeconds">The rest it keeps.</param>
    [Theory]
    [InlineData(30, 15)]
    [InlineData(120, 60)]
    [InlineData(300, 60)]
    public void Every_RestAfterSuccess_NeverExceedsHalfTheInterval(int intervalSeconds, int expectedSeconds)
    {
        JobSchedule.Every(TimeSpan.FromSeconds(intervalSeconds)).RestAfterSuccess(TimeSpan.FromSeconds(60))
            .Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    /// <summary>A rest of zero stays zero for every kind of job.</summary>
    [Fact]
    public void RestAfterSuccess_OfZero_StaysZero()
    {
        JobSchedule.Every(TimeSpan.FromSeconds(30)).RestAfterSuccess(TimeSpan.Zero).Should().Be(TimeSpan.Zero);
        JobSchedule.Daily(5).RestAfterSuccess(TimeSpan.Zero).Should().Be(TimeSpan.Zero);
    }
}
