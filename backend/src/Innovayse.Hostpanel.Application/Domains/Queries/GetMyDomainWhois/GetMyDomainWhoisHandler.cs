using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Queries.GetDomain;
using Innovayse.Hostpanel.Application.Domains.Queries.GetWhois;
using Innovayse.Hostpanel.Shared.Models;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Domains.Queries.GetMyDomainWhois;

/// <summary>
/// Performs a WHOIS lookup on one of the calling client's own domains, refusing every id that is
/// not theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// reach a WHOIS lookup through <see cref="GetMyDomainWhoisQuery"/> without it having run. The
/// two shared reads it then dispatches are the same ones the admin route uses: the domain is
/// loaded to turn the id into a hostname, and only then is the lookup performed.
/// </remarks>
public sealed class GetMyDomainWhoisHandler
{
    /// <summary>The rule that says a client may only touch their own domains.</summary>
    private readonly IDomainOwnership _ownership;

    /// <summary>Wolverine bus, used to reach the shared reads once ownership is settled.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes a new instance of <see cref="GetMyDomainWhoisHandler"/>.</summary>
    /// <param name="ownership">The rule that says a client may only touch their own domains.</param>
    /// <param name="bus">Wolverine bus, used to reach the shared reads once ownership is settled.</param>
    public GetMyDomainWhoisHandler(IDomainOwnership ownership, IMessageBus bus)
    {
        _ownership = ownership;
        _bus = bus;
    }

    /// <summary>Handles <see cref="GetMyDomainWhoisQuery"/>.</summary>
    /// <param name="query">The query. It names no account: this reads the caller's own domain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The WHOIS record for the domain, or <c>DOMAIN_NOT_FOUND</c> when the domain is not the
    /// caller's, when no such domain exists, or when the caller has no client record, or
    /// <c>DOMAIN_WHOIS_NOT_FOUND</c> when the registrar has no record for the name.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result<WhoisDto>> HandleAsync(GetMyDomainWhoisQuery query, CancellationToken ct)
    {
        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(query.DomainId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return Result<WhoisDto>.FailureCode(ownershipOutcome.ErrorCode!);
        }

        var domainOutcome = await _bus.InvokeAsync<Result<DomainDto>>(new GetDomainQuery(query.DomainId), ct);
        if (domainOutcome.IsFailure)
        {
            return Result<WhoisDto>.FailureCode(domainOutcome.ErrorCode!);
        }

        var domain = domainOutcome.Value!;
        return await _bus.InvokeAsync<Result<WhoisDto>>(new GetWhoisQuery(domain.Name), ct);
    }
}
