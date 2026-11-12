using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Queries.GetProductOptionGroups;

/// <summary>Handles <see cref="GetProductOptionGroupsQuery"/>: every group of the product, empty ones included.</summary>
public sealed class GetProductOptionGroupsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    public GetProductOptionGroupsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Handles the query.
    /// </summary>
    /// <param name="query">The product to read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The groups ordered by display order, each with its options (an empty list for a product that has
    /// none), or <c>PRODUCT_NOT_FOUND</c> when the product does not exist, as creating a group answers.
    /// </returns>
    public async Task<Result<IReadOnlyList<ProductOptionGroupDto>>> HandleAsync(
        GetProductOptionGroupsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var productExists = await _context.Products.AnyAsync(p => p.Id == query.ProductId, ct);
        if (!productExists)
        {
            return Result<IReadOnlyList<ProductOptionGroupDto>>.FailureCode(HostpanelErrorCodes.PRODUCT_NOT_FOUND);
        }

        var groups = await _context.ReadProductOptionGroupsAsync(query.ProductId, ct);
        return Result<IReadOnlyList<ProductOptionGroupDto>>.Success(groups);
    }
}
