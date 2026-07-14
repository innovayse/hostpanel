namespace Innovayse.Application.Email.Commands.CreateMailbox;

using Innovayse.Application.Common;
using Innovayse.Application.Email.DTOs;
using Innovayse.Domain.Email.Interfaces;

/// <summary>
/// Handles <see cref="CreateMailboxCommand"/>.
/// Adds the mailbox to the aggregate, provisions it on the mail server, and persists.
/// </summary>
public sealed class CreateMailboxHandler(
    IEmailDomainRepository repo,
    IMailServerClient mailServer,
    IUnitOfWork uow)
{
    /// <summary>
    /// Creates a new mailbox on the specified email domain.
    /// </summary>
    /// <param name="cmd">The create mailbox command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A DTO representing the newly created mailbox.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the email domain is not found or the domain's business rules are violated.
    /// </exception>
    public async Task<MailboxDto> HandleAsync(CreateMailboxCommand cmd, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(cmd.EmailDomainId, ct)
            ?? throw new InvalidOperationException($"Email domain {cmd.EmailDomainId} not found.");

        var mailbox = domain.AddMailbox(cmd.LocalPart, cmd.DisplayName, cmd.QuotaMb);
        var email = mailbox.Email(domain.DomainName);

        await mailServer.CreateMailboxAsync(email, cmd.Password, cmd.DisplayName, cmd.QuotaMb, ct);
        await uow.SaveChangesAsync(ct);

        return MailboxDto.From(mailbox, domain.DomainName);
    }
}
