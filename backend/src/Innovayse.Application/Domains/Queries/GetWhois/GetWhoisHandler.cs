namespace Innovayse.Application.Domains.Queries.GetWhois;

using Innovayse.Application.Domains.DTOs;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>Performs a WHOIS lookup via the registrar provider and returns a <see cref="WhoisDto"/>.</summary>
public sealed class GetWhoisHandler(IRegistrarProvider registrar)
{
    /// <summary>
    /// Handles <see cref="GetWhoisQuery"/>.
    /// </summary>
    /// <param name="query">The get WHOIS query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>WHOIS information for the domain.</returns>
    /// <exception cref="InvalidOperationException">Thrown when WHOIS data is not found for the domain.</exception>
    public async Task<WhoisDto> HandleAsync(GetWhoisQuery query, CancellationToken ct)
    {
        var whois = await registrar.GetWhoisAsync(query.DomainName, ct)
            ?? throw new InvalidOperationException($"WHOIS data not found for domain '{query.DomainName}'.");

        return new WhoisDto(whois.Registrar, whois.Registrant, whois.CreatedAt, whois.ExpiresAt);
    }
}
