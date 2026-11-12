using Innovayse.Hostpanel.Domain.StatusPage;
using Innovayse.Hostpanel.Domain.StatusPage.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.StatusPage;

/// <summary>Unit tests for the monitoring behaviour of <see cref="StatusComponent"/>: checks, threshold, override and incident link.</summary>
public sealed class StatusComponentTests
{
    /// <summary>The moment the sample checks run.</summary>
    private static readonly DateTimeOffset At = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    /// <summary>Creates a component that checks an address and counts <paramref name="threshold"/> failures in a row as down.</summary>
    /// <param name="threshold">How many failed checks in a row make the component count as down.</param>
    /// <returns>A monitored component.</returns>
    private static StatusComponent Monitored(int threshold)
    {
        var component = StatusComponentGroup.Create("Network", 0).AddComponent("Edge", null, 0);
        component.ConfigureMonitoring("https://edge.example.test/health", true, threshold);
        return component;
    }

    /// <summary>A new component is not monitored and tolerates the default number of failures.</summary>
    [Fact]
    public void New_IsNotMonitored_AndHasTheDefaultThreshold()
    {
        var component = StatusComponentGroup.Create("Network", 0).AddComponent("Edge", null, 0);

        Assert.False(component.MonitoringEnabled);
        Assert.Null(component.CheckUrl);
        Assert.Equal(StatusComponent.DefaultFailureThreshold, component.FailureThreshold);
        Assert.Null(component.StateOverride);
        Assert.Null(component.ActiveIncidentId);
        Assert.False(component.IsDown);
    }

    /// <summary>ConfigureMonitoring stores the address, the switch and the threshold.</summary>
    [Fact]
    public void ConfigureMonitoring_StoresTheSettings()
    {
        var component = Monitored(2);

        Assert.Equal("https://edge.example.test/health", component.CheckUrl);
        Assert.True(component.MonitoringEnabled);
        Assert.Equal(2, component.FailureThreshold);
    }

    /// <summary>A monitored component needs an address, but an unmonitored one may have none.</summary>
    [Fact]
    public void ConfigureMonitoring_EnabledWithoutAddress_Throws()
    {
        var component = StatusComponentGroup.Create("Network", 0).AddComponent("Edge", null, 0);

        Assert.Equal(StatusPageErrors.CheckUrlRequired, Assert.Throws<DomainRuleException>(() => component.ConfigureMonitoring(" ", true, 3)).Code);
        component.ConfigureMonitoring(null, false, 3);
        Assert.Null(component.CheckUrl);
    }

    /// <summary>The threshold is from 1 to the largest the component allows.</summary>
    /// <param name="threshold">A threshold outside the range.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(StatusComponent.MaxFailureThreshold + 1)]
    public void ConfigureMonitoring_ThresholdOutOfRange_Throws(int threshold)
    {
        var component = StatusComponentGroup.Create("Network", 0).AddComponent("Edge", null, 0);

        var error = Assert.Throws<DomainRuleException>(() => component.ConfigureMonitoring("https://edge.example.test/health", true, threshold));

        Assert.Equal(StatusPageErrors.FailureThresholdInvalid, error.Code);
    }

    /// <summary>Failures below the threshold leave the component operational; reaching it makes it an outage.</summary>
    [Fact]
    public void RecordCheck_FailuresReachingTheThreshold_MakeAnOutage()
    {
        var component = Monitored(3);

        component.RecordCheck(At, false, 10);
        component.RecordCheck(At, false, 10);
        Assert.Equal(StatusComponentState.Operational, component.CurrentState);
        Assert.False(component.IsDown);

        component.RecordCheck(At, false, 10);
        Assert.Equal(StatusComponentState.Outage, component.CurrentState);
        Assert.True(component.IsDown);
        Assert.Equal(3, component.ConsecutiveFailures);
    }

    /// <summary>A success ends the run of failures, so the failures must be consecutive to count.</summary>
    [Fact]
    public void RecordCheck_ASuccessResetsTheRun_AndRecovers()
    {
        var component = Monitored(2);
        component.RecordCheck(At, false, 10);
        component.RecordCheck(At, false, 10);
        Assert.Equal(StatusComponentState.Outage, component.CurrentState);

        component.RecordCheck(At, true, 10);
        Assert.Equal(StatusComponentState.Operational, component.CurrentState);
        Assert.Equal(0, component.ConsecutiveFailures);

        component.RecordCheck(At, false, 10);
        Assert.Equal(StatusComponentState.Operational, component.CurrentState);
    }

    /// <summary>RecordCheck hands back the record of the check, to be stored.</summary>
    [Fact]
    public void RecordCheck_ReturnsTheRecord()
    {
        var component = Monitored(3);

        var check = component.RecordCheck(At, false, 250);

        Assert.Equal(component.Id, check.ComponentId);
        Assert.Equal(At, check.CheckedAt);
        Assert.False(check.Succeeded);
        Assert.Equal(250, check.LatencyMs);
    }

    /// <summary>An override decides the shown state whatever the checks find, and clearing it hands the state back to them.</summary>
    [Fact]
    public void OverrideState_WinsOverTheChecks_UntilCleared()
    {
        var component = Monitored(1);
        component.OverrideState(StatusComponentState.Maintenance);

        component.RecordCheck(At, false, 10);
        Assert.Equal(StatusComponentState.Maintenance, component.CurrentState);
        Assert.True(component.IsDown);

        component.OverrideState(null);
        Assert.Equal(StatusComponentState.Outage, component.CurrentState);

        component.RecordCheck(At, true, 10);
        Assert.Equal(StatusComponentState.Operational, component.CurrentState);
    }

    /// <summary>An override on a component that is not monitored shows, and clearing it makes the component operational.</summary>
    [Fact]
    public void OverrideState_OnAnUnmonitoredComponent_ShowsThenClears()
    {
        var component = StatusComponentGroup.Create("Network", 0).AddComponent("Edge", null, 0);

        component.OverrideState(StatusComponentState.Degraded);
        Assert.Equal(StatusComponentState.Degraded, component.CurrentState);

        component.OverrideState(null);
        Assert.Equal(StatusComponentState.Operational, component.CurrentState);
    }

    /// <summary>Switching monitoring off forgets the run of failures, so the component is no longer down.</summary>
    [Fact]
    public void ConfigureMonitoring_Disabled_ForgetsTheFailures()
    {
        var component = Monitored(1);
        component.RecordCheck(At, false, 10);
        Assert.Equal(StatusComponentState.Outage, component.CurrentState);

        component.ConfigureMonitoring("https://edge.example.test/health", false, 1);

        Assert.Equal(0, component.ConsecutiveFailures);
        Assert.Equal(StatusComponentState.Operational, component.CurrentState);
    }

    /// <summary>The incident the checks opened is remembered until it is released.</summary>
    [Fact]
    public void TrackIncident_ThenRelease_ClearsTheLink()
    {
        var component = Monitored(1);

        component.TrackIncident(42);
        Assert.Equal(42, component.ActiveIncidentId);

        component.ReleaseIncident();
        Assert.Null(component.ActiveIncidentId);
    }
}
