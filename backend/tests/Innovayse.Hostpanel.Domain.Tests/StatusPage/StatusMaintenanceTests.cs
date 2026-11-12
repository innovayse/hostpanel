using Innovayse.Hostpanel.Domain.StatusPage;
using Innovayse.Hostpanel.Domain.StatusPage.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.StatusPage;

/// <summary>Unit tests for the <see cref="StatusMaintenance"/> aggregate: its schedule, its state and its affected components.</summary>
public sealed class StatusMaintenanceTests
{
    /// <summary>The moment the sample window starts.</summary>
    private static readonly DateTimeOffset Start = new(2026, 11, 20, 22, 0, 0, TimeSpan.Zero);

    /// <summary>Builds a window from <see cref="Start"/> for two hours that affects components 1 and 2.</summary>
    /// <returns>A new window.</returns>
    private static StatusMaintenance Sample()
    {
        return StatusMaintenance.Schedule("Network upgrade", "Short interruptions.", Start, Start.AddHours(2), [1, 2]);
    }

    /// <summary>Schedule stores the announcement and the affected components, each once.</summary>
    [Fact]
    public void Schedule_StoresTheAnnouncement_AndEachComponentOnce()
    {
        var window = StatusMaintenance.Schedule("Network upgrade", "Short interruptions.", Start, Start.AddHours(2), [1, 2, 2]);

        Assert.Equal("Network upgrade", window.Title);
        Assert.Equal("Short interruptions.", window.Description);
        Assert.Equal(Start, window.ScheduledStart);
        Assert.Equal(Start.AddHours(2), window.ScheduledEnd);
        Assert.Null(window.CancelledAt);
        Assert.Equal([1, 2], window.Components.Select(c => c.ComponentId).Order());
    }

    /// <summary>A window needs a title.</summary>
    /// <param name="title">A title that is empty or whitespace.</param>
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Schedule_WithoutTitle_Throws(string title)
    {
        var ex = Assert.Throws<DomainRuleException>(() => StatusMaintenance.Schedule(title, null, Start, Start.AddHours(1), [1]));

        Assert.Equal(StatusPageErrors.MaintenanceTitleRequired, ex.Code);
    }

    /// <summary>A window must end after it starts.</summary>
    /// <param name="endOffsetMinutes">How far the end lies from the start; zero or negative is refused.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void Schedule_EndNotAfterStart_Throws(int endOffsetMinutes)
    {
        var ex = Assert.Throws<DomainRuleException>(
            () => StatusMaintenance.Schedule("Upgrade", null, Start, Start.AddMinutes(endOffsetMinutes), [1]));

        Assert.Equal(StatusPageErrors.MaintenanceWindowInvalid, ex.Code);
    }

    /// <summary>The state follows the clock: scheduled before the start, in progress from it, completed from the end.</summary>
    [Fact]
    public void StateAt_FollowsTheClock()
    {
        var window = Sample();

        Assert.Equal(StatusMaintenanceState.Scheduled, window.StateAt(Start.AddMinutes(-1)));
        Assert.Equal(StatusMaintenanceState.InProgress, window.StateAt(Start));
        Assert.Equal(StatusMaintenanceState.InProgress, window.StateAt(Start.AddHours(2).AddTicks(-1)));
        Assert.Equal(StatusMaintenanceState.Completed, window.StateAt(Start.AddHours(2)));
    }

    /// <summary>Cancelling stamps the moment and makes the window cancelled whatever the clock says.</summary>
    [Fact]
    public void Cancel_MakesTheWindowCancelled()
    {
        var window = Sample();
        var now = Start.AddMinutes(-10);

        window.Cancel(now);

        Assert.Equal(now, window.CancelledAt);
        Assert.Equal(StatusMaintenanceState.Cancelled, window.StateAt(Start.AddHours(1)));
    }

    /// <summary>A window that ended or was cancelled takes neither a cancellation nor a change.</summary>
    [Fact]
    public void Cancel_AndUpdate_AfterTheEndOrACancellation_Throw()
    {
        var ended = Sample();
        var cancelled = Sample();
        cancelled.Cancel(Start.AddMinutes(-5));
        var after = Start.AddHours(3);

        Assert.Equal(StatusPageErrors.MaintenanceAlreadyClosed, Assert.Throws<DomainRuleException>(() => ended.Cancel(after)).Code);
        Assert.Equal(
            StatusPageErrors.MaintenanceAlreadyClosed,
            Assert.Throws<DomainRuleException>(() => cancelled.Update("Again", null, Start, Start.AddHours(1), [1], Start.AddMinutes(-4))).Code);
    }

    /// <summary>Update replaces the announcement and the component set, keeping the components that stay.</summary>
    [Fact]
    public void Update_ReplacesTheAnnouncement_AndTheComponentSet()
    {
        var window = Sample();
        var kept = window.Components.Single(c => c.ComponentId == 2);

        window.Update("Moved", null, Start.AddDays(1), Start.AddDays(1).AddHours(1), [2, 3], Start.AddHours(-1));

        Assert.Equal("Moved", window.Title);
        Assert.Null(window.Description);
        Assert.Equal(Start.AddDays(1), window.ScheduledStart);
        Assert.Equal([2, 3], window.Components.Select(c => c.ComponentId).Order());
        Assert.Same(kept, window.Components.Single(c => c.ComponentId == 2));
    }

    /// <summary>A change that breaks a rule is refused and leaves the window as it was.</summary>
    [Fact]
    public void Update_WithInvalidWindow_Throws_AndChangesNothing()
    {
        var window = Sample();

        var ex = Assert.Throws<DomainRuleException>(() => window.Update("Moved", null, Start, Start.AddHours(-1), [1], Start.AddHours(-1)));

        Assert.Equal(StatusPageErrors.MaintenanceWindowInvalid, ex.Code);
        Assert.Equal("Network upgrade", window.Title);
        Assert.Equal([1, 2], window.Components.Select(c => c.ComponentId).Order());
    }
}
