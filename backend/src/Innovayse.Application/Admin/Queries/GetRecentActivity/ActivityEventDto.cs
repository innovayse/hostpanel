namespace Innovayse.Application.Admin.Queries.GetRecentActivity;

/// <summary>
/// A single event surfaced in the admin notification feed.
/// </summary>
/// <param name="Type">Machine-readable event kind, used by the frontend to pick an icon/label.</param>
/// <param name="Message">Human-readable summary of the event.</param>
/// <param name="OccurredAt">When the event happened (client registration, invoice due date, domain expiry).</param>
/// <param name="EntityId">
/// Primary key of the client, invoice or domain the event is about, so the frontend can link
/// to it. A domain's own id has no dedicated top-level route (only nested under its client),
/// so the frontend falls back to the domains list for that type regardless of this value.
/// </param>
public record ActivityEventDto(ActivityEventType Type, string Message, DateTimeOffset OccurredAt, int EntityId);
