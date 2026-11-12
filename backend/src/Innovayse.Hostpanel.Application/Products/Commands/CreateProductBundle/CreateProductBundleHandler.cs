using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Products.Commands.CreateProductBundle;

/// <summary>Creates a product bundle and persists it.</summary>
public sealed class CreateProductBundleHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    public CreateProductBundleHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Handles the command.
    /// </summary>
    /// <param name="cmd">The bundle to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The new bundle's id, <c>PRODUCT_BUNDLE_PRODUCT_NOT_FOUND</c> when a component's product does not exist, or
    /// <c>PRODUCT_BUNDLE_DOMAIN_PRODUCT_NOT_ALLOWED</c> when one is a domain product.
    /// </returns>
    public async Task<Result<int>> HandleAsync(CreateProductBundleCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var refusal = await _context.FindBundleItemRefusalAsync(cmd.Items, ct);
        if (refusal is not null)
        {
            return Result<int>.FailureCode(refusal);
        }

        var bundle = ProductBundle.Create(
            cmd.Name,
            cmd.Description,
            cmd.SortOrder,
            cmd.IncludesDomain,
            cmd.DomainYears,
            cmd.DiscountType,
            cmd.DiscountAmount,
            cmd.DiscountCurrency,
            cmd.Items.ToItems());
        _context.ProductBundles.Add(bundle);
        await _context.SaveChangesAsync(ct);
        return Result<int>.Success(bundle.Id);
    }
}
