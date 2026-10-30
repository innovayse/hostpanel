using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Commands.UpdateProduct;

/// <summary>Updates an existing product's details and prices.</summary>
public sealed class UpdateProductHandler
{
    /// <summary>Amount written to a legacy price column when the product has no base-currency price for that cycle.</summary>
    private const decimal NoPrice = 0m;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the base currency the legacy price fields and columns are in.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="payerCurrency">Resolves the base currency the legacy price fields and columns are in.</param>
    public UpdateProductHandler(IHostpanelDbContext context, IPayerCurrencyResolver payerCurrency)
    {
        _context = context;
        _payerCurrency = payerCurrency;
    }

    /// <summary>
    ///     Handles the command.
    /// </summary>
    /// <param name="cmd">The product's new details and prices.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, <c>PRODUCT_NOT_FOUND</c> when the id names no product, or
    /// <c>PRODUCT_SKU_ALREADY_IN_USE</c> when another product already holds the SKU.
    /// </returns>
    public async Task<Result> HandleAsync(UpdateProductCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var product = await _context.Products.Include(p => p.Prices).FirstOrDefaultAsync(p => p.Id == cmd.Id, ct);
        if (product is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.PRODUCT_NOT_FOUND);
        }

        // A null SKU keeps the stored one, so only a SKU actually sent is checked; a blank one clears.
        if (cmd.Sku is not null && await _context.ProductSkuInUseAsync(cmd.Sku, product.Id, ct))
        {
            return Result.FailureCode(HostpanelErrorCodes.PRODUCT_SKU_ALREADY_IN_USE);
        }

        var prices = await _payerCurrency.ResolveProductPricesAsync(
            cmd.Prices, cmd.MonthlyPrice, cmd.AnnualPrice, ct);
        product.ReplacePrices(prices.CurrencyPrices, prices.ReplaceAllCurrencies);

        // The legacy single-currency columns are kept one release so a rollback and the admin
        // services grid still read real numbers: they mirror the base-currency prices as stored
        // after this save, so a legacy-form save that touched only one cycle is reflected too.
        product.Update(
            cmd.Name, cmd.Description, cmd.Website, cmd.Slug, cmd.PackageName,
            product.PriceFor(prices.BaseCurrencyCode, BillingCycle.Monthly) ?? NoPrice,
            product.PriceFor(prices.BaseCurrencyCode, BillingCycle.Annual) ?? NoPrice,
            cmd.ServerGroupId,
            cmd.IsFeatured,
            cmd.SortOrder);

        if (cmd.Sku is not null)
        {
            product.SetSku(cmd.Sku);
        }

        if (cmd.Metadata is not null)
        {
            product.SetMetadata(cmd.Metadata);
        }

        if (cmd.GracePeriodDays is { } gracePeriodDays)
        {
            product.SetGracePeriodDays(gracePeriodDays);
        }

        if (cmd.IsFeatured == true)
        {
            await product.UnfeatureSiblingsAsync(_context, ct);
        }

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (cmd.Sku is not null && product.Sku is { } sku)
        {
            // Another writer took the SKU between the check above and this save, and the unique index
            // refused. Read before the discard below puts the stored SKU back on the product, and asked
            // again rather than assumed: any other failed save is not this conflict and must still
            // surface as the error it is.
            _context.DiscardPendingChanges();
            if (await _context.ProductSkuInUseAsync(sku, product.Id, ct))
            {
                return Result.FailureCode(HostpanelErrorCodes.PRODUCT_SKU_ALREADY_IN_USE);
            }

            throw;
        }

        return Result.Success();
    }
}
