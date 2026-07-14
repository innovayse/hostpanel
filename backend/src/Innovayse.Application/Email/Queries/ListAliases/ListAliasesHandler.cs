namespace Innovayse.Application.Email.Queries.ListAliases;

using Innovayse.Application.Email.DTOs;
using Innovayse.Domain.Email.Interfaces;

/// <summary>
/// Handles <see cref="ListAliasesQuery"/>.
/// Loads the email domain aggregate and projects its aliases into DTOs.
/// </summary>
public sealed class ListAliasesHandler(IEmailDomainRepository repo)
{
    /// <summary>
    /// Returns all mail aliases belonging to the specified email domain.
    /// </summary>
    /// <param name="query">The list aliases query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of mail alias DTOs.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the email domain is not found.
    /// </exception>
    public async Task<IReadOnlyList<MailAliasDto>> HandleAsync(ListAliasesQuery query, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(query.EmailDomainId, ct)
            ?? throw new InvalidOperationException($"Email domain {query.EmailDomainId} not found.");

        return domain.Aliases.Select(MailAliasDto.From).ToList();
    }
}
