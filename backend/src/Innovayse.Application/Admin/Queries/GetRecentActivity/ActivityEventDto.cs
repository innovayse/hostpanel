namespace Innovayse.Application.Admin.Queries.GetRecentActivity;

/// <summary>
/// A single event surfaced in the admin notification feed.
/// </summary>
/// <param name="Type">Machine-readable event kind, used by the frontend to pick an icon/label.</param>
/// <param name="Message">Human-readable summary of the event.</param>
/// <param name="OccurredAt">When the event happened (client registration, invoice due date, domain expiry).</param>
public record ActivityEventDto(ActivityEventType Type, string Message, DateTimeOffset OccurredAt);
