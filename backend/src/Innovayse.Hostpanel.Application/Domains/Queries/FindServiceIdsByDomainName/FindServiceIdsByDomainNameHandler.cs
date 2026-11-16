using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Domains.Queries.FindServiceIdsByDomainName;

/// <summary>
/// Handles <see cref="FindServiceIdsByDomainNameQuery"/>: matches the text against every linked domain's name, so a
/// service with several linked domains is found by any of them.
/// </summary>
public sealed class FindServiceIdsByDomainNameHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public FindServiceIdsByDomainNameHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The text to look for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The distinct ids of the services with a matching linked domain, in no particular order. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<int>>> HandleAsync(FindServiceIdsByDomainNameQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.Search))
        {
            return Result<IReadOnlyList<int>>.Success([]);
        }

        IReadOnlyList<int> serviceIds = await _context.Domains
            .AsNoTracking()
            .Where(d => d.LinkedServiceId != null)
            .Search(query.Search, d => d.Name)
            .Select(d => d.LinkedServiceId!.Value)
            .Distinct()
            .ToListAsync(ct);

        return Result<IReadOnlyList<int>>.Success(serviceIds);
    }
}
