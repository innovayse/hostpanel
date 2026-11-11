using Innovayse.Hostpanel.Domain.StatusPage.Enums;
using Innovayse.Hostpanel.SharedKernel.Domain;

namespace Innovayse.Hostpanel.Domain.StatusPage;

/// <summary>
/// One entry of an incident's timeline: what the operator told the visitors at a moment, and the state
/// the incident was in then. Stored in the <c>StatusIncidentUpdates</c> table.
/// </summary>
public sealed class StatusIncidentUpdate : BaseEntity
{
    /// <summary>Longest Message may be: matches the <c>StatusIncidentUpdates.Message</c> column.</summary>
    public const int MessageMaxLength = 4000;

    /// <summary>Gets the id of the incident the entry belongs to.</summary>
    public int IncidentId { get; private set; }

    /// <summary>Gets what the operator told the visitors.</summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>Gets the state the incident moved to with this entry.</summary>
    public StatusIncidentState State { get; private set; }

    /// <summary>Gets the UTC moment the entry was posted.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private StatusIncidentUpdate() { }

    /// <summary>Creates a timeline entry.</summary>
    /// <param name="message">What the operator tells the visitors.</param>
    /// <param name="state">The state the incident moves to with this entry.</param>
    /// <param name="postedAt">The UTC moment the entry is posted.</param>
    /// <returns>A new entry, not yet attached to an incident.</returns>
    internal static StatusIncidentUpdate Create(string message, StatusIncidentState state, DateTimeOffset postedAt)
    {
        return new StatusIncidentUpdate { Message = message, State = state, CreatedAt = postedAt };
    }
}
