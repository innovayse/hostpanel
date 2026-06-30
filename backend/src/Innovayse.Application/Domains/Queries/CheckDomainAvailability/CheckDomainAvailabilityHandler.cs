namespace Innovayse.Application.Domains.Queries.CheckDomainAvailability;

using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Checks whether a domain name is available for registration.
/// First checks the local database — if the domain is already registered through us it is not available.
/// Falls back to the registrar provider API for external availability.
/// </summary>
public sealed class CheckDomainAvailabilityHandler(IRegistrarProvider registrar, IDomainRepository repo)
{
    /// <summary>
    /// Handles <see cref="CheckDomainAvailabilityQuery"/>.
    /// </summary>
    /// <param name="query">The check domain availability query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> if the domain is available; otherwise <see langword="false"/>.</returns>
    public async Task<bool> HandleAsync(CheckDomainAvailabilityQuery query, CancellationToken ct)
    {
        // If the domain already exists in our system, it's not available
        var existing = await repo.FindByNameAsync(query.DomainName, ct);
        if (existing is not null)
        {
            return false;
        }

        return await registrar.CheckAvailabilityAsync(query.DomainName, ct);
    }
}
