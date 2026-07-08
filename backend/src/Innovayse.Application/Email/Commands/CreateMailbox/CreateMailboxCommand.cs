namespace Innovayse.Application.Email.Commands.CreateMailbox;

/// <summary>
/// Creates a new mailbox on the given email domain and provisions it on the mail server.
/// </summary>
public record CreateMailboxCommand(
    int EmailDomainId,
    string LocalPart,
    string DisplayName,
    string Password,
    int QuotaMb = 512);
