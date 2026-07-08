namespace Innovayse.Application.Email.Queries.ListEmailDomains;

using Innovayse.Application.Email.DTOs;
using Innovayse.Domain.Email.Interfaces;

/// <summary>Handles <see cref="ListEmailDomainsQuery"/>.</summary>
public sealed class ListEmailDomainsHandler(IEmailDomainRepository repo)
{
    /// <summary>
    /// Returns all email domains owned by the specified client.
    /// </summary>
    public async Task<IReadOnlyList<EmailDomainDto>> HandleAsync(ListEmailDomainsQuery query, CancellationToken ct)
    {
        var domains = await repo.ListByClientAsync(query.ClientId, ct);
        return domains.Select(EmailDomainDto.From).ToList();
    }
}
