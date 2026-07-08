namespace Innovayse.Application.Email.Commands.DeleteAlias;

/// <summary>
/// Deletes a mail alias from the given email domain and deprovisions it on the mail server.
/// </summary>
public record DeleteAliasCommand(int EmailDomainId, int AliasId);
