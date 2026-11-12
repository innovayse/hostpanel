using Innovayse.Hostpanel.Domain.StatusPage.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;

namespace Innovayse.Hostpanel.Domain.StatusPage;

/// <summary>
/// An incident announced on the status page, with the timeline of updates that follows it from the first
/// report to the all clear. Stored in the <c>StatusIncidents</c> table.
/// </summary>
public sealed class StatusIncident : BaseEntity, IAggregateRoot
{
    /// <summary>Longest Title may be: matches the <c>StatusIncidents.Title</c> column.</summary>
    public const int TitleMaxLength = 300;

    /// <summary>Backing field for <see cref="Updates"/>.</summary>
    private readonly List<StatusIncidentUpdate> _updates = [];

    /// <summary>Gets the headline visitors see.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>Gets where the incident stands now.</summary>
    public StatusIncidentState State { get; private set; }

    /// <summary>Gets the UTC moment the incident was announced.</summary>
    public DateTimeOffset StartedAt { get; private set; }

    /// <summary>Gets the UTC moment the incident was resolved, or <c>null</c> while it is open.</summary>
    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>Gets the timeline entries, in the order they were posted.</summary>
    public IReadOnlyList<StatusIncidentUpdate> Updates => _updates.AsReadOnly();

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private StatusIncident() { }

    /// <summary>Announces an incident: it starts <see cref="StatusIncidentState.Investigating"/> with a first timeline entry.</summary>
    /// <param name="title">The headline visitors see.</param>
    /// <param name="message">The first thing the visitors are told.</param>
    /// <param name="startedAt">The UTC moment the incident is announced.</param>
    /// <returns>A new, open incident.</returns>
    /// <exception cref="DomainRuleException">The title or the message is empty.</exception>
    public static StatusIncident Open(string title, string message, DateTimeOffset startedAt)
    {
        var incident = new StatusIncident { State = StatusIncidentState.Investigating, StartedAt = startedAt };
        incident.Retitle(title);
        incident.PostUpdate(StatusIncidentState.Investigating, message, startedAt);
        return incident;
    }

    /// <summary>Changes the headline visitors see.</summary>
    /// <param name="title">The new headline.</param>
    /// <exception cref="DomainRuleException">The title is empty.</exception>
    public void Retitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainRuleException(StatusPageErrors.IncidentTitleRequired, "An incident needs a title.");
        }

        Title = title;
    }

    /// <summary>
    /// Adds a timeline entry and moves the incident to its state. Moving to
    /// <see cref="StatusIncidentState.Resolved"/> closes the incident.
    /// </summary>
    /// <param name="state">The state the incident moves to.</param>
    /// <param name="message">What the visitors are told.</param>
    /// <param name="postedAt">The UTC moment the entry is posted.</param>
    /// <exception cref="DomainRuleException">The message is empty, or the incident is already resolved.</exception>
    public void PostUpdate(StatusIncidentState state, string message, DateTimeOffset postedAt)
    {
        if (State == StatusIncidentState.Resolved)
        {
            throw new DomainRuleException(StatusPageErrors.IncidentAlreadyResolved, "The incident is resolved and takes no more updates.");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new DomainRuleException(StatusPageErrors.IncidentMessageRequired, "An update needs a message.");
        }

        _updates.Add(StatusIncidentUpdate.Create(message, state, postedAt));
        State = state;
        if (state == StatusIncidentState.Resolved)
        {
            ResolvedAt = postedAt;
        }
    }
}
