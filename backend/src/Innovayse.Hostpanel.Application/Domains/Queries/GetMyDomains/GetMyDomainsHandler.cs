using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Domains.Registrar;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Domains.Queries.GetMyDomains;

/// <summary>Returns every domain of the account the caller acts for as <see cref="MyDomainDto"/> items.</summary>
public sealed class GetMyDomainsHandler
{
    /// <summary>The data context the caller's domains are read from.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the account the caller acts for, owner or linked user; the query does not say, and must not.</summary>
    private readonly IClientAccess _access;

    /// <summary>Tells which optional operations the registrar that holds each domain offers.</summary>
    private readonly RegistrarProviderResolver _registrars;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The data context the caller's domains are read from.</param>
    /// <param name="access">Resolves the account the caller acts for, owner or linked user; the query does not say, and must not.</param>
    /// <param name="registrars">Tells which optional operations the registrar that holds each domain offers.</param>
    public GetMyDomainsHandler(IHostpanelDbContext context, IClientAccess access, RegistrarProviderResolver registrars)
    {
        _context = context;
        _access = access;
        _registrars = registrars;
    }

    /// <summary>
    /// Handles <see cref="GetMyDomainsQuery"/>.
    /// </summary>
    /// <param name="query">The query. It names no account: this reads the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// All domains for the client, including nameservers and DNS records, or
    /// <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller acts for no account.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the request carries no subject.</exception>
    public async Task<Result<IReadOnlyList<MyDomainDto>>> HandleAsync(GetMyDomainsQuery query, CancellationToken ct)
    {
        var clientId = await _access.RequireClientIdAsync(ct);
        if (clientId.IsFailure)
        {
            return Result<IReadOnlyList<MyDomainDto>>.FailureCode(clientId.ErrorCode!);
        }

        var domains = await _context.Domains
            .IncludeChildren()
            .Where(x => x.ClientId == clientId.Value)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        var dtos = new List<MyDomainDto>(domains.Count);
        foreach (var domain in domains)
        {
            dtos.Add(domain.ToMyDto(await _registrars.ResolveCapabilitiesAsync(domain, ct)));
        }

        return Result<IReadOnlyList<MyDomainDto>>.Success(dtos);
    }
}
