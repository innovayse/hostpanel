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
    public async Task RequireOwnedByCallerAsync(int domainId, CancellationToken ct)
    {
        var userId = caller.RequireUserId();

        var client = await clients.FindByUserIdAsync(userId, ct);
        if (client is not null)
        {
            var domain = await domains.FindByIdAsync(domainId, ct);
            if (domain is not null && domain.ClientId == client.Id)
            {
                return;
            }
        }

        // A domain that does not exist, a domain belonging to somebody else, and a caller with no
        // client record all land here and answer identically. Distinguishing them would turn this
        // route into a way of enumerating ids -- and domain ids are sequential integers.
        throw new DomainNotFoundException(domainId);
    }
}
