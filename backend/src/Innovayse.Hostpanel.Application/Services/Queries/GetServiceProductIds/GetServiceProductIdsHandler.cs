using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Services.Queries.GetServiceProductIds;

/// <summary>Handles <see cref="GetServiceProductIdsQuery"/> with one read.</summary>
public sealed class GetServiceProductIdsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public GetServiceProductIdsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Reads the products.</summary>
    /// <param name="query">The services.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The distinct products of the existing services, ordered by id. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<int>>> HandleAsync(GetServiceProductIdsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.ServiceIds.Count == 0)
        {
            return Result<IReadOnlyList<int>>.Success([]);
        }

        var productIds = await _context.ClientServices.AsNoTracking()
            .Where(s => query.ServiceIds.Contains(s.Id))
            .Select(s => s.ProductId)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync(ct);

        return Result<IReadOnlyList<int>>.Success(productIds);
    }
}
