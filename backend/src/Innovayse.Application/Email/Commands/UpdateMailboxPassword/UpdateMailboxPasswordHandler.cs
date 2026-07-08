namespace Innovayse.Application.Email.Commands.UpdateMailboxPassword;

using Innovayse.Domain.Email.Interfaces;

/// <summary>
/// Handles <see cref="UpdateMailboxPasswordCommand"/>.
/// Delegates the password change to the mail server; no aggregate state changes are needed.
/// </summary>
public sealed class UpdateMailboxPasswordHandler(
    IEmailDomainRepository repo,
    IMailServerClient mailServer)
{
    /// <summary>
    /// Updates the password for an existing mailbox.
    /// </summary>
    /// <param name="cmd">The update password command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the email domain or mailbox is not found.
    /// </exception>
    public async Task HandleAsync(UpdateMailboxPasswordCommand cmd, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(cmd.EmailDomainId, ct)
            ?? throw new InvalidOperationException($"Email domain {cmd.EmailDomainId} not found.");

        var mailbox = domain.Mailboxes.FirstOrDefault(m => m.Id == cmd.MailboxId)
            ?? throw new InvalidOperationException($"Mailbox {cmd.MailboxId} not found.");

        await mailServer.UpdateMailboxPasswordAsync(mailbox.Email(domain.DomainName), cmd.NewPassword, ct);
    }
}
