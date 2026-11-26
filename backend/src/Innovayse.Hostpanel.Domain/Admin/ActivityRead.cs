namespace Innovayse.Hostpanel.Domain.Admin;

/// <summary>
/// The record that one admin has read one event of the notification feed. The feed is computed from live
/// data, so an event has no row of its own to carry a read flag; it is identified by a key built from what it is
/// about and when it happened, and this row remembers that the admin has seen it. One row per admin and event.
/// Created via <see cref="Create"/>: no public constructor.
/// </summary>
public sealed class ActivityRead
{
    /// <summary>The longest event key a row can hold.</summary>
    public const int MaxEventKeyLength = 100;

    /// <summary>Gets the subject of the admin who read the event, taken from the token.</summary>
    public string UserId { get; private set; } = string.Empty;

    /// <summary>Gets the key of the event that was read (see <c>ActivityEventDto.Key</c>).</summary>
    public string EventKey { get; private set; } = string.Empty;

    /// <summary>Gets when the admin read the event.</summary>
    public DateTimeOffset ReadAt { get; private set; }

    /// <summary>EF Core parameterless constructor: do not call directly.</summary>
    private ActivityRead() { }

    /// <summary>Records that an admin has read an event.</summary>
    /// <param name="userId">The subject of the admin who read it.</param>
    /// <param name="eventKey">The key of the event.</param>
    /// <param name="readAt">The moment it was read.</param>
    /// <returns>A new, unpersisted <see cref="ActivityRead"/>.</returns>
    public static ActivityRead Create(string userId, string eventKey, DateTimeOffset readAt)
    {
        return new ActivityRead { UserId = userId, EventKey = eventKey, ReadAt = readAt };
    }
}
