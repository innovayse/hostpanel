using Innovayse.Hostpanel.Application.Admin.Common.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Common;

/// <summary>
/// A single event surfaced in the admin notification feed. It carries the kind of event and the facts
/// about it, never a sentence: the admin words the title in the operator's language from the type and these facts.
/// </summary>
/// <param name="Type">Machine-readable event kind, which decides the icon, the colour, the wording and the link.</param>
/// <param name="OccurredAt">When the event happened (client registration, invoice due date, payment, ticket opening, domain expiry).</param>
/// <param name="EntityId">
/// Primary key of the client, invoice, ticket or domain the event is about, so the frontend can link
/// to it. A domain's own id has no dedicated top-level route (only nested under its client),
/// so the frontend falls back to the domains list for that type regardless of this value.
/// </param>
/// <param name="ClientId">Primary key of the client the event concerns (the new client itself for a registration).</param>
/// <param name="ClientName">Full name of that client.</param>
/// <param name="Subject">The domain name of a domain event or the subject of a ticket; <see langword="null"/> for every other type.</param>
/// <param name="Amount">The invoice total of an overdue invoice or a payment; <see langword="null"/> for every other type.</param>
/// <param name="Currency">Currency code of <paramref name="Amount"/>; <see langword="null"/> when there is no amount.</param>
/// <param name="IsRead">Whether the calling admin has already read the event.</param>
public sealed record ActivityEventDto(
    ActivityEventType Type,
    DateTimeOffset OccurredAt,
    int EntityId,
    int ClientId,
    string ClientName,
    string? Subject,
    decimal? Amount,
    string? Currency,
    bool IsRead)
{
    /// <summary>
    /// The identity of the event: its kind, the record it is about and the moment it happened. The same event has
    /// the same key on every read of the feed, which is what a read mark is stored against.
    /// </summary>
    public string Key => $"{Type}:{EntityId}:{OccurredAt.UtcTicks}";
}
