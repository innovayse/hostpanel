using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Queries.ListCatalogProductBundles;

/// <summary>
/// Returns the storefront catalogue's product bundles: active bundles only, without the fields only
/// staff should see.
/// </summary>
public sealed class ListCatalogProductBundlesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    public ListCatalogProductBundlesHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Handles the query.
    /// </summary>
    /// <param name="qry">The query; it carries no input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The active bundles whose products are all on sale, by sort position, then name. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<CatalogProductBundleDto>>> HandleAsync(
        ListCatalogProductBundlesQuery qry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(qry);

        var bundles = await _context.ProductBundles
            .AsNoTracking()
            .Include(b => b.Items)
            .Where(b => b.IsActive)
            // A bundle offering a product that is no longer on sale is not offered at all.
            .Where(b => b.Items.All(i => _context.Products.Any(p => p.Id == i.ProductId && p.Status == ProductStatus.Active)))
            .OrderBy(b => b.SortOrder)
            .ThenBy(b => b.Name)
            .ToListAsync(ct);
        var names = await _context.ProductNamesAsync(bundles, ct);

        return Result<IReadOnlyList<CatalogProductBundleDto>>.Success(bundles.Select(b => b.ToCatalogDto(names)).ToList());
    }
}
