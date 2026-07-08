namespace Innovayse.Application.Email.Queries.ListMailboxes;

using Innovayse.Application.Email.DTOs;
using Innovayse.Domain.Email.Interfaces;

/// <summary>
/// Handles <see cref="ListMailboxesQuery"/>.
/// Loads the email domain aggregate and projects its mailboxes into DTOs.
/// </summary>
public sealed class ListMailboxesHandler(IEmailDomainRepository repo)
{
    /// <summary>
    /// Returns all mailboxes belonging to the specified email domain.
    /// </summary>
    /// <param name="query">The list mailboxes query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of mailbox DTOs.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the email domain is not found.
    /// </exception>
    public async Task<IReadOnlyList<MailboxDto>> HandleAsync(ListMailboxesQuery query, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(query.EmailDomainId, ct)
            ?? throw new InvalidOperationException($"Email domain {query.EmailDomainId} not found.");

        return domain.Mailboxes.Select(m => MailboxDto.From(m, domain.DomainName)).ToList();
    }
}
