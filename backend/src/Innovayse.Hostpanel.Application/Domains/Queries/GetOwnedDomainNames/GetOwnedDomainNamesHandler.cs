using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Domains.Queries.GetOwnedDomainNames;

/// <summary>Handles <see cref="GetOwnedDomainNamesQuery"/>: reads the client's matching domains in one pass.</summary>
public sealed class GetOwnedDomainNamesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetOwnedDomainNamesHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The client and the names to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The checked names the client holds, in lower case, in no particular order. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<string>>> HandleAsync(GetOwnedDomainNamesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Domain names are case-insensitive, and a cart may send any case.
        var names = query.DomainNames.Select(n => n.ToLowerInvariant()).Distinct().ToList();
        if (names.Count == 0)
        {
            return Result<IReadOnlyList<string>>.Success([]);
        }

        var domains = _context.Domains
            .AsNoTracking()
            .Where(d => d.ClientId == query.ClientId && names.Contains(d.Name.ToLower()));
        if (query.LiveOnly)
        {
            // A registration that lapsed, was cancelled or moved away is not a domain the client holds in the sense
            // an order line needs: counting it would mark a line done that never delivered a domain.
            domains = domains.Where(d => d.Status == DomainStatus.Active
                || d.Status == DomainStatus.PendingRegistration
                || d.Status == DomainStatus.PendingTransfer);
        }

        IReadOnlyList<string> owned = await domains.Select(d => d.Name.ToLower()).ToListAsync(ct);

        return Result<IReadOnlyList<string>>.Success(owned);
    }
}
