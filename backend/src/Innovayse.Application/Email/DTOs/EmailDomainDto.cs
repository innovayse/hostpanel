namespace Innovayse.Application.Email.DTOs;

using Innovayse.Domain.Email;

/// <summary>
/// Read model returned by email domain queries and command handlers.
/// </summary>
public record EmailDomainDto(
    int Id,
    int ClientId,
    string DomainName,
    string Status,
    int MaxMailboxes,
    int MaxAliases,
    int MaxQuotaMb,
    int MailboxCount,
    int AliasCount,
    DateTimeOffset? DnsVerifiedAt,
    DateTimeOffset CreatedAt)
{
    /// <summary>Maps an <see cref="EmailDomain"/> aggregate to a flat DTO.</summary>
    public static EmailDomainDto From(EmailDomain e) => new(
        e.Id, e.ClientId, e.DomainName, e.Status.ToString(),
        e.MaxMailboxes, e.MaxAliases, e.MaxQuotaMb,
        e.Mailboxes.Count, e.Aliases.Count,
        e.DnsVerifiedAt, e.CreatedAt);
}
