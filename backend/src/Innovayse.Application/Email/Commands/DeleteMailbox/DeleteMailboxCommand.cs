namespace Innovayse.Application.Email.Commands.DeleteMailbox;

/// <summary>
/// Deletes a mailbox from the given email domain and deprovisions it on the mail server.
/// </summary>
public record DeleteMailboxCommand(int EmailDomainId, int MailboxId);
