namespace Innovayse.Application.Domains.Queries.GetMyDomainNameservers;

using Innovayse.Application.Domains.Common;
using Innovayse.Application.Domains.Queries.GetDomain;
using Wolverine;

/// <summary>
/// Returns the nameservers of one of the calling client's own domains, refusing every id that is
/// not theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// read a nameserver list through <see cref="GetMyDomainNameserversQuery"/> without it having
/// run. Once ownership is settled the projection is the same read the admin route performs, so
/// this dispatches <see cref="GetDomainQuery"/> and narrows the result rather than growing a
/// second copy of the mapping that could drift from it.
/// </remarks>
/// <param name="ownership">The rule that says a client may only touch their own domains.</param>
/// <param name="bus">Wolverine bus, used to reach the shared read once ownership is settled.</param>
public sealed class GetMyDomainNameserversHandler(IDomainOwnership ownership, IMessageBus bus)
{
    /// <summary>Handles <see cref="GetMyDomainNameserversQuery"/>.</summary>
    /// <param name="query">The query. It names no account: this reads the caller's own domain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ordered nameserver list for the domain.</returns>
    /// <exception cref="DomainNotFoundException">
    /// Thrown when the domain is not the caller's, when no such domain exists, and when the
    /// caller has no client record -- all with the same wording and status.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<IReadOnlyList<NameserverDto>> HandleAsync(
        GetMyDomainNameserversQuery query, CancellationToken ct)
    {
        await ownership.RequireOwnedByCallerAsync(query.DomainId, ct);

        var domain = await bus.InvokeAsync<DomainDto>(new GetDomainQuery(query.DomainId), ct);
        return domain.Nameservers;
    }
}
