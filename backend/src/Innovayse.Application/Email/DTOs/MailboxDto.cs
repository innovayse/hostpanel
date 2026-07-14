namespace Innovayse.Application.Email.DTOs;

using Innovayse.Domain.Email;

/// <summary>
/// Read model for a mailbox returned by mailbox queries and command handlers.
/// </summary>
public record MailboxDto(
    int Id,
    string LocalPart,
    string Email,
    string DisplayName,
    int QuotaMb,
    bool IsActive,
    DateTimeOffset CreatedAt)
{
    /// <summary>Maps a <see cref="Mailbox"/> entity to a flat DTO.</summary>
    public static MailboxDto From(Mailbox m, string domainName) =>
        new(m.Id, m.LocalPart, m.Email(domainName), m.DisplayName,
            m.QuotaMb, m.IsActive, m.CreatedAt);
}
