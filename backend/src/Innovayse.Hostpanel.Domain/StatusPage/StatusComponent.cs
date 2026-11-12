using Innovayse.Hostpanel.Domain.StatusPage.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.StatusPage;

/// <summary>
/// One service or piece of infrastructure the status page reports on. A component belongs to a
/// <see cref="StatusComponentGroup"/>, which creates it. Stored in the <c>StatusComponents</c> table.
/// </summary>
public sealed class StatusComponent : BaseEntity
{
    /// <summary>Longest Name may be: matches the <c>StatusComponents.Name</c> column.</summary>
    public const int NameMaxLength = 200;

    /// <summary>Longest Description may be: matches the <c>StatusComponents.Description</c> column.</summary>
    public const int DescriptionMaxLength = 1000;

    /// <summary>Longest CheckUrl may be: matches the <c>StatusComponents.CheckUrl</c> column.</summary>
    public const int CheckUrlMaxLength = 2000;

    /// <summary>How many failed checks in a row a new component tolerates before it counts as down.</summary>
    public const int DefaultFailureThreshold = 3;

    /// <summary>The most failed checks in a row a component may be set to tolerate.</summary>
    public const int MaxFailureThreshold = 10;

    /// <summary>Gets the id of the group the component is listed under.</summary>
    public int GroupId { get; private set; }

    /// <summary>Gets the component's display name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Gets a short explanation of what the component covers, or <c>null</c>.</summary>
    public string? Description { get; private set; }

    /// <summary>Gets the position within its group (ascending).</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>
    /// Gets the state the status page shows for the component now: <see cref="StateOverride"/> when an operator set one,
    /// otherwise what the automatic checks found.
    /// </summary>
    public StatusComponentState CurrentState { get; private set; }

    /// <summary>Gets the address the automatic check requests, or <c>null</c> when none is set.</summary>
    public string? CheckUrl { get; private set; }

    /// <summary>Gets whether the automatic check runs for the component.</summary>
    public bool MonitoringEnabled { get; private set; }

    /// <summary>Gets how many failed checks in a row make the component count as down.</summary>
    public int FailureThreshold { get; private set; } = DefaultFailureThreshold;

    /// <summary>Gets how many checks in a row have failed; 0 after a check succeeded.</summary>
    public int ConsecutiveFailures { get; private set; }

    /// <summary>Gets the state an operator forces onto the page whatever the checks find, or <c>null</c> for the automatic state.</summary>
    public StatusComponentState? StateOverride { get; private set; }

    /// <summary>Gets the id of the incident the automatic checks opened and have not closed yet, or <c>null</c>.</summary>
    public int? ActiveIncidentId { get; private set; }

    /// <summary>Gets whether the monitored component has failed enough checks in a row to count as down.</summary>
    public bool IsDown => MonitoringEnabled && ConsecutiveFailures >= FailureThreshold;

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private StatusComponent() { }

    /// <summary>Creates a component that starts <see cref="StatusComponentState.Operational"/>.</summary>
    /// <param name="name">The display name.</param>
    /// <param name="description">A short explanation of what the component covers, or <c>null</c>.</param>
    /// <param name="displayOrder">The position within its group (ascending).</param>
    /// <returns>A new component, not yet attached to a group.</returns>
    internal static StatusComponent Create(string name, string? description, int displayOrder)
    {
        return new StatusComponent
        {
            Name = name,
            Description = description,
            DisplayOrder = displayOrder,
            CurrentState = StatusComponentState.Operational,
        };
    }

    /// <summary>
    /// Forces a state onto the status page whatever the automatic checks find, or hands the state back to them.
    /// </summary>
    /// <param name="state">The state to force, or <c>null</c> to show what the checks find.</param>
    public void OverrideState(StatusComponentState? state)
    {
        StateOverride = state;
        ApplyState();
    }

    /// <summary>Sets the address the automatic check requests, whether it runs, and how many failures in a row it tolerates.</summary>
    /// <param name="checkUrl">The address to request, or <c>null</c> for none.</param>
    /// <param name="enabled">Whether the automatic check runs.</param>
    /// <param name="failureThreshold">How many failed checks in a row make the component count as down.</param>
    /// <exception cref="DomainRuleException">The check is enabled without an address, or the threshold is out of range.</exception>
    public void ConfigureMonitoring(string? checkUrl, bool enabled, int failureThreshold)
    {
        if (enabled && string.IsNullOrWhiteSpace(checkUrl))
        {
            throw new DomainRuleException(StatusPageErrors.CheckUrlRequired, "A monitored component needs an address to check.");
        }

        if (failureThreshold < 1 || failureThreshold > MaxFailureThreshold)
        {
            throw new DomainRuleException(StatusPageErrors.FailureThresholdInvalid, "The failure threshold is out of range.");
        }

        CheckUrl = string.IsNullOrWhiteSpace(checkUrl) ? null : checkUrl;
        MonitoringEnabled = enabled;
        FailureThreshold = failureThreshold;
        if (!enabled)
        {
            ConsecutiveFailures = 0;
        }

        ApplyState();
    }

    /// <summary>Takes the outcome of one automatic check into account: the run of failures and the state the page shows.</summary>
    /// <param name="checkedAt">The UTC moment the check ran.</param>
    /// <param name="succeeded">Whether the component answered as expected.</param>
    /// <param name="latencyMs">How long the answer took, in milliseconds.</param>
    /// <returns>The record of the check, to be stored.</returns>
    public StatusCheck RecordCheck(DateTimeOffset checkedAt, bool succeeded, int latencyMs)
    {
        ConsecutiveFailures = succeeded ? 0 : ConsecutiveFailures + 1;
        ApplyState();
        return StatusCheck.Create(Id, checkedAt, succeeded, latencyMs);
    }

    /// <summary>Remembers the incident the automatic checks opened for the component.</summary>
    /// <param name="incidentId">The incident's id.</param>
    public void TrackIncident(int incidentId)
    {
        ActiveIncidentId = incidentId;
    }

    /// <summary>Forgets the incident the automatic checks opened, once it is closed.</summary>
    public void ReleaseIncident()
    {
        ActiveIncidentId = null;
    }

    /// <summary>Works out the state the page shows: the override when there is one, an outage while the component is down, operational otherwise.</summary>
    private void ApplyState()
    {
        CurrentState = StateOverride ?? (IsDown ? StatusComponentState.Outage : StatusComponentState.Operational);
    }
}
