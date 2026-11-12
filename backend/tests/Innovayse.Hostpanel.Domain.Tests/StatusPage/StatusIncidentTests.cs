using Innovayse.Hostpanel.Domain.StatusPage;
using Innovayse.Hostpanel.Domain.StatusPage.Enums;

namespace Innovayse.Hostpanel.Domain.Tests.StatusPage;

/// <summary>Unit tests for the <see cref="StatusIncident"/> aggregate's timeline.</summary>
public sealed class StatusIncidentTests
{
    /// <summary>The moment the sample incident is announced.</summary>
    private static readonly DateTimeOffset Start = new(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

    /// <summary>Open starts the incident investigating, with its first timeline entry.</summary>
    [Fact]
    public void Open_StartsInvestigating_WithTheFirstEntry()
    {
        var incident = StatusIncident.Open("Slow panel", "Looking into it.", Start);

        Assert.Equal("Slow panel", incident.Title);
        Assert.Equal(StatusIncidentState.Investigating, incident.State);
        Assert.Equal(Start, incident.StartedAt);
        Assert.Null(incident.ResolvedAt);
        var entry = Assert.Single(incident.Updates);
        Assert.Equal("Looking into it.", entry.Message);
        Assert.Equal(StatusIncidentState.Investigating, entry.State);
        Assert.Equal(Start, entry.CreatedAt);
    }

    /// <summary>An incident needs a title and a first message.</summary>
    [Fact]
    public void Open_WithoutTitleOrMessage_Throws()
    {
        Assert.Equal(StatusPageErrors.IncidentTitleRequired, Assert.Throws<DomainRuleException>(() => StatusIncident.Open(" ", "Message", Start)).Code);
        Assert.Equal(StatusPageErrors.IncidentMessageRequired, Assert.Throws<DomainRuleException>(() => StatusIncident.Open("Title", "", Start)).Code);
    }

    /// <summary>An update adds an entry and moves the incident to its state, and leaves it open.</summary>
    [Fact]
    public void PostUpdate_AddsAnEntry_AndMovesTheState()
    {
        var incident = StatusIncident.Open("Slow panel", "Looking into it.", Start);

        incident.PostUpdate(StatusIncidentState.Monitoring, "Fix is out.", Start.AddMinutes(20));

        Assert.Equal(StatusIncidentState.Monitoring, incident.State);
        Assert.Null(incident.ResolvedAt);
        Assert.Equal(2, incident.Updates.Count);
        Assert.Equal("Fix is out.", incident.Updates[1].Message);
    }

    /// <summary>Resolving stamps the resolution moment.</summary>
    [Fact]
    public void PostUpdate_Resolved_StampsTheResolutionMoment()
    {
        var incident = StatusIncident.Open("Slow panel", "Looking into it.", Start);
        var resolvedAt = Start.AddHours(1);

        incident.PostUpdate(StatusIncidentState.Resolved, "All good.", resolvedAt);

        Assert.Equal(StatusIncidentState.Resolved, incident.State);
        Assert.Equal(resolvedAt, incident.ResolvedAt);
    }

    /// <summary>A resolved incident takes no more updates, and the refused one leaves no trace.</summary>
    [Fact]
    public void PostUpdate_AfterResolved_Throws()
    {
        var incident = StatusIncident.Open("Slow panel", "Looking into it.", Start);
        incident.PostUpdate(StatusIncidentState.Resolved, "All good.", Start.AddHours(1));

        var ex = Assert.Throws<DomainRuleException>(() => incident.PostUpdate(StatusIncidentState.Monitoring, "Again.", Start.AddHours(2)));

        Assert.Equal(StatusPageErrors.IncidentAlreadyResolved, ex.Code);
        Assert.Equal(2, incident.Updates.Count);
    }

    /// <summary>An update needs a message, and the refused one leaves the incident untouched.</summary>
    [Fact]
    public void PostUpdate_WithoutMessage_Throws()
    {
        var incident = StatusIncident.Open("Slow panel", "Looking into it.", Start);

        var ex = Assert.Throws<DomainRuleException>(() => incident.PostUpdate(StatusIncidentState.Identified, " ", Start.AddMinutes(5)));

        Assert.Equal(StatusPageErrors.IncidentMessageRequired, ex.Code);
        Assert.Equal(StatusIncidentState.Investigating, incident.State);
        Assert.Single(incident.Updates);
    }

    /// <summary>Retitle changes the headline, whatever the state, and refuses an empty one.</summary>
    [Fact]
    public void Retitle_ChangesTheTitle_AndRefusesAnEmptyOne()
    {
        var incident = StatusIncident.Open("Slow panel", "Looking into it.", Start);
        incident.PostUpdate(StatusIncidentState.Resolved, "All good.", Start.AddHours(1));

        incident.Retitle("Panel latency");

        Assert.Equal("Panel latency", incident.Title);
        Assert.Equal(StatusPageErrors.IncidentTitleRequired, Assert.Throws<DomainRuleException>(() => incident.Retitle(" ")).Code);
        Assert.Equal("Panel latency", incident.Title);
    }
}
