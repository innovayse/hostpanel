using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Queries.GetProductBundle;

/// <summary>Returns one product bundle with its components.</summary>
public sealed class GetProductBundleHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    public GetProductBundleHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Handles the query.
    /// </summary>
    /// <param name="qry">Which bundle to read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The bundle, or <c>PRODUCT_BUNDLE_NOT_FOUND</c> when the id names none.</returns>
    public async Task<Result<ProductBundleDto>> HandleAsync(GetProductBundleQuery qry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(qry);

        var bundle = await _context.ProductBundles
            .AsNoTracking()
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == qry.Id, ct);
        if (bundle is null)
        {
            return Result<ProductBundleDto>.FailureCode(HostpanelErrorCodes.PRODUCT_BUNDLE_NOT_FOUND);
        }

        var names = await _context.ProductNamesAsync([bundle], ct);
        return Result<ProductBundleDto>.Success(bundle.ToDto(names));
    }
}
