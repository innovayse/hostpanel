namespace Innovayse.Application.Email.Queries.GetEmailDomain;

using Innovayse.Application.Email.DTOs;
using Innovayse.Domain.Email.Interfaces;

/// <summary>Handles <see cref="GetEmailDomainQuery"/>.</summary>
public sealed class GetEmailDomainHandler(IEmailDomainRepository repo)
{
    /// <summary>
    /// Returns the email domain with the given ID, or <see langword="null"/> if not found.
    /// </summary>
    public async Task<EmailDomainDto?> HandleAsync(GetEmailDomainQuery query, CancellationToken ct)
    {
        var domain = await repo.FindByIdAsync(query.Id, ct);
        return domain is null ? null : EmailDomainDto.From(domain);
    }
}
