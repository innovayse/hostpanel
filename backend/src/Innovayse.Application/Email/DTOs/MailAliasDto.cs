namespace Innovayse.Application.Email.DTOs;

using Innovayse.Domain.Email;

/// <summary>
/// Read model for a mail alias returned by alias queries and command handlers.
/// </summary>
public record MailAliasDto(
    int Id,
    string SourceAddress,
    string DestinationAddress,
    bool IsActive,
    DateTimeOffset CreatedAt)
{
    /// <summary>Maps a <see cref="MailAlias"/> entity to a flat DTO.</summary>
    public static MailAliasDto From(MailAlias a) =>
        new(a.Id, a.SourceAddress, a.DestinationAddress, a.IsActive, a.CreatedAt);
}
