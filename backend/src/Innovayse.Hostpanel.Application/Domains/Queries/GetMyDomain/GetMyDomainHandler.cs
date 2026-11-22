using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Domains.Queries.GetMyDomain;

/// <summary>
/// Returns one of the calling client's own domains, refusing every id that is not theirs.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint, so it travels with the message: nothing can
/// read a domain through <see cref="GetMyDomainQuery"/> without it having run. The projection is
/// the client's own <see cref="MyDomainDto"/>, never the staff read, so no registrar reference,
/// transfer code or staff note reaches the client.
/// </remarks>
public sealed class GetMyDomainHandler
{
    /// <summary>The rule that says a client may only touch their own domains.</summary>
    private readonly IDomainOwnership _ownership;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Tells which optional operations the registrar that holds the domain offers.</summary>
    private readonly RegistrarProviderResolver _registrars;

    /// <summary>Initializes a new instance of <see cref="GetMyDomainHandler"/>.</summary>
    /// <param name="ownership">The rule that says a client may only touch their own domains.</param>
    /// <param name="context">The module's data context.</param>
    /// <param name="registrars">Tells which optional operations the registrar that holds the domain offers.</param>
    public GetMyDomainHandler(IDomainOwnership ownership, IHostpanelDbContext context, RegistrarProviderResolver registrars)
    {
        _ownership = ownership;
        _context = context;
        _registrars = registrars;
    }

    /// <summary>Handles <see cref="GetMyDomainQuery"/>.</summary>
    /// <param name="query">The query. It names no account: this reads the caller's own domain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The matching <see cref="MyDomainDto"/>, or <c>DOMAIN_NOT_FOUND</c> when the domain is not the
    /// caller's, when no such domain exists, or when the caller has no client record.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result<MyDomainDto>> HandleAsync(GetMyDomainQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var ownershipOutcome = await _ownership.RequireOwnedByCallerAsync(query.DomainId, ct);
        if (ownershipOutcome.IsFailure)
        {
            return Result<MyDomainDto>.FailureCode(ownershipOutcome.ErrorCode!);
        }

        var domain = await _context.Domains
            .IncludeChildren()
            .FirstOrDefaultAsync(d => d.Id == query.DomainId, ct);
        if (domain is null)
        {
            return Result<MyDomainDto>.FailureCode(HostpanelErrorCodes.DOMAIN_NOT_FOUND);
        }

        return Result<MyDomainDto>.Success(domain.ToMyDto(await _registrars.ResolveCapabilitiesAsync(domain, ct)));
    }
}
