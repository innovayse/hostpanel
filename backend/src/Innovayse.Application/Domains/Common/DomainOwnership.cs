namespace Innovayse.Application.Domains.Common;

using Innovayse.Application.Common;
using Innovayse.Domain.Clients.Interfaces;
using Innovayse.Domain.Domains.Interfaces;

/// <summary>
/// Resolves domain ownership against the client the current credential names.
/// </summary>
/// <param name="domains">Domain repository.</param>
/// <param name="clients">Client repository, for mapping the caller's subject to their account.</param>
/// <param name="caller">Who is asking. Nothing tells this type whose domains to consider.</param>
public sealed class DomainOwnership(
    IDomainRepository domains,
    IClientRepository clients,
    ICurrentRequestContext caller) : IDomainOwnership
{
    /// <inheritdoc/>
    public async Task<bool> IsOwnedByCallerAsync(int domainId, CancellationToken ct)
    {
        var userId = caller.RequireUserId();

        var client = await clients.FindByUserIdAsync(userId, ct);
        if (client is null)
        {
            return false;
        }

        var domain = await domains.FindByIdAsync(domainId, ct);

        // A domain that does not exist and a domain belonging to somebody else are the same
        // answer here. Distinguishing them would turn this into a way of enumerating ids.
        return domain is not null && domain.ClientId == client.Id;
    }
}
