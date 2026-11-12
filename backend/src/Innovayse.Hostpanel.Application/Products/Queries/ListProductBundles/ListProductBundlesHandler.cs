using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Queries.ListProductBundles;

/// <summary>Returns every product bundle with its components.</summary>
public sealed class ListProductBundlesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    public ListProductBundlesHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Handles the query.
    /// </summary>
    /// <param name="qry">The query; it carries no input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The bundles by sort position, then name. Never refuses.</returns>
    public async Task<Result<IReadOnlyList<ProductBundleDto>>> HandleAsync(ListProductBundlesQuery qry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(qry);

        var bundles = await _context.ProductBundles
            .AsNoTracking()
            .Include(b => b.Items)
            .OrderBy(b => b.SortOrder)
            .ThenBy(b => b.Name)
            .ToListAsync(ct);
        var names = await _context.ProductNamesAsync(bundles, ct);

        return Result<IReadOnlyList<ProductBundleDto>>.Success(bundles.Select(b => b.ToDto(names)).ToList());
    }
}
