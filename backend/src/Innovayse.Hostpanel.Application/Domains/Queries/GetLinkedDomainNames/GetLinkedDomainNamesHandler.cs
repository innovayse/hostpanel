using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Common;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Domains.Queries.GetLinkedDomainNames;

/// <summary>Handles <see cref="GetLinkedDomainNamesQuery"/>: reads the linked domains in one pass.</summary>
public sealed class GetLinkedDomainNamesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetLinkedDomainNamesHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The client services to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>One entry per linked service, in no particular order. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<LinkedDomainNameDto>>> HandleAsync(
        GetLinkedDomainNamesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.ServiceIds.Count == 0)
        {
            return Result<IReadOnlyList<LinkedDomainNameDto>>.Success([]);
        }

        // Read-only, so no tracking is needed. A service can have several registered domains linked, and it shows one:
        // the first by name, so the same domain is shown on every read.
        IReadOnlyList<LinkedDomainNameDto> links = await _context.Domains
            .AsNoTracking()
            .Where(d => d.LinkedServiceId != null && query.ServiceIds.Contains(d.LinkedServiceId.Value))
            .GroupBy(d => d.LinkedServiceId!.Value)
            .Select(g => new LinkedDomainNameDto(g.Key, g.Min(d => d.Name)!))
            .ToListAsync(ct);

        return Result<IReadOnlyList<LinkedDomainNameDto>>.Success(links);
    }
}
