namespace Innovayse.Application.Domains.Queries.GetMyDomain;

using Innovayse.Application.Domains.Common;
using Innovayse.Application.Domains.Queries.GetDomain;
using Wolverine;

/// <summary>
/// Returns one of the calling client's own domains, refusing every id that is not theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// read a domain through <see cref="GetMyDomainQuery"/> without it having run. Once ownership is
/// settled the work is the same the admin route performs, so this dispatches
/// <see cref="GetDomainQuery"/> rather than duplicating it.
/// </remarks>
/// <param name="ownership">The rule that says a client may only touch their own domains.</param>
/// <param name="bus">Wolverine bus, used to reach the shared use case once ownership is settled.</param>
public sealed class GetMyDomainHandler(IDomainOwnership ownership, IMessageBus bus)
{
    /// <summary>Handles <see cref="GetMyDomainQuery"/>.</summary>
    /// <param name="query">The query. It names no account: this reads the caller's own domain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="DomainDto"/>.</returns>
    /// <exception cref="DomainNotFoundException">
    /// Thrown when the domain is not the caller's, when no such domain exists, and when the caller
    /// has no client record -- all with the same wording and status.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<DomainDto> HandleAsync(GetMyDomainQuery query, CancellationToken ct)
    {
        await ownership.RequireOwnedByCallerAsync(query.DomainId, ct);
        return await bus.InvokeAsync<DomainDto>(new GetDomainQuery(query.DomainId), ct);
    }
}
