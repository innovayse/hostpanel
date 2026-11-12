using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Commands.UpdateProductBundle;

/// <summary>Replaces a product bundle's details and components.</summary>
public sealed class UpdateProductBundleHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    public UpdateProductBundleHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>
    ///     Handles the command.
    /// </summary>
    /// <param name="cmd">The bundle's new details and components.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, <c>PRODUCT_BUNDLE_NOT_FOUND</c> when the id names no bundle,
    /// <c>PRODUCT_BUNDLE_PRODUCT_NOT_FOUND</c> when a component's product does not exist, or
    /// <c>PRODUCT_BUNDLE_DOMAIN_PRODUCT_NOT_ALLOWED</c> when one is a domain product.
    /// </returns>
    public async Task<Result> HandleAsync(UpdateProductBundleCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var bundle = await _context.ProductBundles.Include(b => b.Items).FirstOrDefaultAsync(b => b.Id == cmd.Id, ct);
        if (bundle is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.PRODUCT_BUNDLE_NOT_FOUND);
        }

        var refusal = await _context.FindBundleItemRefusalAsync(cmd.Items, ct);
        if (refusal is not null)
        {
            return Result.FailureCode(refusal);
        }

        bundle.Update(
            cmd.Name,
            cmd.Description,
            cmd.SortOrder,
            cmd.IncludesDomain,
            cmd.DomainYears,
            cmd.DiscountType,
            cmd.DiscountAmount,
            cmd.DiscountCurrency,
            cmd.Items.ToItems());
        if (cmd.IsActive is true)
        {
            bundle.Activate();
        }
        else if (cmd.IsActive is false)
        {
            bundle.Deactivate();
        }

        await _context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
