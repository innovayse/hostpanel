namespace Innovayse.Hostpanel.Domain.Notifications.Enums;

/// <summary>Where one email log row stands in its delivery.</summary>
/// <remarks>
/// Stored as its number: never renumber a member, rows already hold it. A keyed delivery moves
/// <see cref="Sending"/> to <see cref="Sent"/> or <see cref="Failed"/>; an unkeyed one is written
/// straight in its outcome.
/// </remarks>
public enum EmailDeliveryState
{
    /// <summary>The delivery is claimed and the relay has not answered yet.</summary>
    Sending = 1,

    /// <summary>The relay accepted the message; it is never sent again under the same key.</summary>
    Sent = 2,

    /// <summary>The relay refused the message or could not be reached; a retry may send it again.</summary>
    Failed = 3,
}
