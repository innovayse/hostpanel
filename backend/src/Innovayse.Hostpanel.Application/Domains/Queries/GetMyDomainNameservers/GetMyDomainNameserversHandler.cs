using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Queries.GetDomain;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Domains.Queries.GetMyDomainNameservers;

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
public sealed class GetMyDomainNameserversHandler
{
    /// <summary>The rule that says a client may only touch their own domains.</summary>
    private readonly IDomainOwnership _ownership;

    /// <summary>Wolverine bus, used to reach the shared read once ownership is settled.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes a new instance of <see cref="GetMyDomainNameserversHandler"/>.</summary>
    /// <param name="ownership">The rule that says a client may only touch their own domains.</param>
    /// <param name="bus">Wolverine bus, used to reach the shared read once ownership is settled.</param>
    public GetMyDomainNameserversHandler(IDomainOwnership ownership, IMessageBus bus)
    {
        _ownership = ownership;
        _bus = bus;
    }

    /// <summary>Handles <see cref="GetMyDomainNameserversQuery"/>.</summary>
    /// <param name="query">The query. It names no account: this reads the caller's own domain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The ordered nameserver list for the domain, or <c>DOMAIN_NOT_FOUND</c> when the domain is
    /// not the caller's, when no such domain exists, or when the caller has no client record.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result<IReadOnlyList<NameserverDto>>> HandleAsync(
        GetMyDomainNameserversQuery query, CancellationToken ct)
    {
        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(query.DomainId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return Result<IReadOnlyList<NameserverDto>>.FailureCode(ownershipOutcome.ErrorCode!);
        }

        var domainOutcome = await _bus.InvokeAsync<Result<DomainDto>>(new GetDomainQuery(query.DomainId), ct);
        if (domainOutcome.IsFailure)
        {
            return Result<IReadOnlyList<NameserverDto>>.FailureCode(domainOutcome.ErrorCode!);
        }

        return Result.Success(domainOutcome.Value!.Nameservers);
    }
}
