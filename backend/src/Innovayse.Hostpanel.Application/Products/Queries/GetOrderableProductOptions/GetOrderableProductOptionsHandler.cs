using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Queries.GetOrderableProductOptions;

/// <summary>
/// Handles <see cref="GetOrderableProductOptionsQuery"/>: the groups the order screen shows for an
/// active product, in the shape the admin reads, with the groups that have nothing to choose left out.
/// </summary>
public sealed class GetOrderableProductOptionsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    public GetOrderableProductOptionsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Handles the query.
    /// </summary>
    /// <param name="query">The product being ordered.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The groups ordered by display order that hold at least one option, or <c>PRODUCT_NOT_FOUND</c> when
    /// the product does not exist or is not active, so an inactive product is not told apart from a missing one.
    /// </returns>
    public async Task<Result<IReadOnlyList<ProductOptionGroupDto>>> HandleAsync(
        GetOrderableProductOptionsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var isOrderable = await _context.Products
            .AnyAsync(p => p.Id == query.ProductId && p.Status == ProductStatus.Active, ct);
        if (!isOrderable)
        {
            return Result<IReadOnlyList<ProductOptionGroupDto>>.FailureCode(HostpanelErrorCodes.PRODUCT_NOT_FOUND);
        }

        var groups = await _context.ReadProductOptionGroupsAsync(query.ProductId, ct);
        return Result<IReadOnlyList<ProductOptionGroupDto>>.Success(groups.Where(g => g.Options.Count > 0).ToList());
    }
}
