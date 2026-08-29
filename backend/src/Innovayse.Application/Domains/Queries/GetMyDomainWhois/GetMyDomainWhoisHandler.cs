namespace Innovayse.Application.Domains.Queries.GetMyDomainWhois;

using Innovayse.Application.Domains.Common;
using Innovayse.Application.Domains.Queries.GetDomain;
using Innovayse.Application.Domains.Queries.GetWhois;
using Wolverine;

/// <summary>
/// Performs a WHOIS lookup on one of the calling client's own domains, refusing every id that is
/// not theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// reach a WHOIS lookup through <see cref="GetMyDomainWhoisQuery"/> without it having run. The
/// two shared reads it then dispatches are the same ones the admin route uses -- the domain is
/// loaded to turn the id into a hostname, and only then is the lookup performed.
/// </remarks>
/// <param name="ownership">The rule that says a client may only touch their own domains.</param>
/// <param name="bus">Wolverine bus, used to reach the shared reads once ownership is settled.</param>
public sealed class GetMyDomainWhoisHandler(IDomainOwnership ownership, IMessageBus bus)
{
    /// <summary>Handles <see cref="GetMyDomainWhoisQuery"/>.</summary>
    /// <param name="query">The query. It names no account: this reads the caller's own domain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The WHOIS record for the domain.</returns>
    /// <exception cref="DomainNotFoundException">
    /// Thrown when the domain is not the caller's, when no such domain exists, and when the
    /// caller has no client record -- all with the same wording and status.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<WhoisDto> HandleAsync(GetMyDomainWhoisQuery query, CancellationToken ct)
    {
        await ownership.RequireOwnedByCallerAsync(query.DomainId, ct);

        var domain = await bus.InvokeAsync<DomainDto>(new GetDomainQuery(query.DomainId), ct);
        return await bus.InvokeAsync<WhoisDto>(new GetWhoisQuery(domain.Name + domain.Tld), ct);
    }
}
