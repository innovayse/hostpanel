namespace Innovayse.Application.Email.Commands.UpdateMailboxPassword;

/// <summary>
/// Updates the password for an existing mailbox on the mail server.
/// </summary>
public record UpdateMailboxPasswordCommand(int EmailDomainId, int MailboxId, string NewPassword);
