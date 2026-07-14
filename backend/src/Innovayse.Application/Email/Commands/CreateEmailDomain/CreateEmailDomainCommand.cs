namespace Innovayse.Application.Email.Commands.CreateEmailDomain;

/// <summary>
/// Provisions a new email domain on the mail server and persists it in the database.
/// </summary>
public record CreateEmailDomainCommand(
    int ClientId,
    string DomainName,
    int MaxMailboxes = 10,
    int MaxAliases = 50,
    int MaxQuotaMb = 5120,
    int? HostpanelDomainId = null);
