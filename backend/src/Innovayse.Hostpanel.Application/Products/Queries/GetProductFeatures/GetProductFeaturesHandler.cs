using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Queries.GetProductFeatures;

/// <summary>
/// Handles <see cref="GetProductFeaturesQuery"/> by resolving the matching products first and
/// reading their lines in one pass, so the storefront can build a comparison table from a single
/// request rather than one per plan.
/// </summary>
public sealed class GetProductFeaturesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    public GetProductFeaturesHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Handles the query.
    /// </summary>
    /// <param name="query">The product filters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The specification lines, ordered by product then by sort order. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<ProductFeatureDto>>> HandleAsync(
        GetProductFeaturesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var products = _context.Products.AsNoTracking().AsQueryable();
        if (query.GroupId.HasValue)
        {
            products = products.Where(p => p.GroupId == query.GroupId.Value);
        }

        if (query.ActiveOnly)
        {
            products = products.Where(p => p.Status == ProductStatus.Active);
        }

        if (query.ProductId.HasValue)
        {
            products = products.Where(p => p.Id == query.ProductId.Value);
        }

        var ids = products.Select(p => p.Id);

        var lines = await _context.ProductFeatures
            .AsNoTracking()
            .Where(f => ids.Contains(f.ProductId))
            .OrderBy(f => f.ProductId)
            .ThenBy(f => f.SortOrder)
            .Select(f => new ProductFeatureDto(f.Id, f.ProductId, f.Label, f.Value, f.SortOrder))
            .ToListAsync(ct);

        return Result<IReadOnlyList<ProductFeatureDto>>.Success(lines);
    }
}
