namespace Innovayse.Application.Domains.Queries.CheckDomainAvailability;

using Innovayse.Domain.Domains.Interfaces;

/// <summary>Checks whether a domain name is available for registration via the registrar provider.</summary>
public sealed class CheckDomainAvailabilityHandler(IRegistrarProvider registrar)
{
    /// <summary>
    /// Handles <see cref="CheckDomainAvailabilityQuery"/>.
    /// </summary>
    /// <param name="query">The check domain availability query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> if the domain is available; otherwise <see langword="false"/>.</returns>
    public async Task<bool> HandleAsync(CheckDomainAvailabilityQuery query, CancellationToken ct)
    {
        return await registrar.CheckAvailabilityAsync(query.DomainName, ct);
    }
}
