namespace Innovayse.Application.Email.Commands.DeleteMailbox;

using Innovayse.Application.Common;
using Innovayse.Domain.Email.Interfaces;

/// <summary>
/// Handles <see cref="DeleteMailboxCommand"/>.
/// Deprovisions the mailbox on the mail server, removes it from the aggregate, and persists.
/// </summary>
public sealed class DeleteMailboxHandler(
    IEmailDomainRepository repo,
    IMailServerClient mailServer,
    IUnitOfWork uow)
{
    /// <summary>
    /// Deletes an existing mailbox from the specified email domain.
    /// </summary>
    /// <param name="cmd">The delete mailbox command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the email domain or mailbox is not found.
    /// </exception>
    public async Task HandleAsync(DeleteMailboxCommand cmd, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(cmd.EmailDomainId, ct)
            ?? throw new InvalidOperationException($"Email domain {cmd.EmailDomainId} not found.");

        var mailbox = domain.Mailboxes.FirstOrDefault(m => m.Id == cmd.MailboxId)
            ?? throw new InvalidOperationException($"Mailbox {cmd.MailboxId} not found.");

        var email = mailbox.Email(domain.DomainName);
        await mailServer.DeleteMailboxAsync(email, ct);

        domain.RemoveMailbox(cmd.MailboxId);
        await uow.SaveChangesAsync(ct);
    }
}
