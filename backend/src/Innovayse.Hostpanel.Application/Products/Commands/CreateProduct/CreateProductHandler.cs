using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Products.Common;
using Innovayse.Hostpanel.Application.Products.Extensions;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Products.Commands.CreateProduct;

/// <summary>Creates a new product in a product group and persists it.</summary>
public sealed class CreateProductHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the base currency the legacy price fields and columns are in.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>Reads the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>
    ///     Initializes the handler.
    /// </summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="payerCurrency">Resolves the base currency the legacy price fields and columns are in.</param>
    /// <param name="clock">Reads the current moment.</param>
    public CreateProductHandler(IHostpanelDbContext context, IPayerCurrencyResolver payerCurrency, IClock clock)
    {
        _context = context;
        _payerCurrency = payerCurrency;
        _clock = clock;
    }

    /// <summary>
    ///     Handles the command.
    /// </summary>
    /// <param name="cmd">The product to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The newly created product id, <c>PRODUCT_GROUP_NOT_FOUND</c> when the group does not exist, or
    /// <c>PRODUCT_SKU_ALREADY_IN_USE</c> when another product already holds the SKU.
    /// </returns>
    public async Task<Result<int>> HandleAsync(CreateProductCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var groupExists = await _context.ProductGroups.AnyAsync(g => g.Id == cmd.GroupId, ct);
        if (!groupExists)
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.PRODUCT_GROUP_NOT_FOUND);
        }

        if (cmd.Sku is not null && await _context.ProductSkuInUseAsync(cmd.Sku, excludingId: null, ct))
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.PRODUCT_SKU_ALREADY_IN_USE);
        }

        var prices = await _payerCurrency.ResolveProductPricesAsync(
            cmd.Prices, cmd.MonthlyPrice, cmd.AnnualPrice, ct);

        // The legacy single-currency columns are kept one release so a rollback and the admin
        // services grid still read real numbers: they mirror the base-currency prices.
        var product = Product.Create(
            cmd.GroupId, cmd.Name, cmd.Description, cmd.Website, cmd.Slug, cmd.PackageName, cmd.Type,
            prices.BaseMonthly, prices.BaseAnnual, _clock.UtcNow, cmd.ServerGroupId, cmd.IsFeatured, cmd.SortOrder);
        product.ReplacePrices(prices.CurrencyPrices, prices.ReplaceAllCurrencies);
        product.SetSku(cmd.Sku);
        product.SetMetadata(cmd.Metadata);
        if (cmd.GracePeriodDays is { } gracePeriodDays)
        {
            product.SetGracePeriodDays(gracePeriodDays);
        }

        if (cmd.IsFeatured)
        {
            await product.UnfeatureSiblingsAsync(_context, ct);
        }

        _context.Products.Add(product);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (product.Sku is { } sku)
        {
            // Another writer took the SKU between the check above and this save, and the unique index
            // refused. Asked again rather than assumed: any other failed save is not this conflict and
            // must still surface as the error it is.
            _context.DiscardPendingChanges();
            if (await _context.ProductSkuInUseAsync(sku, excludingId: null, ct))
            {
                return Result<int>.FailureCode(HostpanelErrorCodes.PRODUCT_SKU_ALREADY_IN_USE);
            }

            throw;
        }

        return Result<int>.Success(product.Id);
    }
}
