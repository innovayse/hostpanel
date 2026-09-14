namespace Innovayse.Application.Products.Commands.UpdateProduct;

using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Products.Common;
using Innovayse.Application.Products.Extensions;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;

/// <summary>Updates an existing product's details and prices.</summary>
/// <param name="repo">Product repository the product is read from.</param>
/// <param name="uow">Unit of work for persistence.</param>
/// <param name="payerCurrency">Resolves the base currency the legacy price fields and columns are in.</param>
public sealed class UpdateProductHandler(IProductRepository repo, IUnitOfWork uow, IPayerCurrencyResolver payerCurrency)
{
    /// <summary>Amount written to a legacy price column when the product has no base-currency price for that cycle.</summary>
    private const decimal NoPrice = 0m;

    /// <summary>
    /// Handles <see cref="UpdateProductCommand"/>.
    /// </summary>
    /// <param name="cmd">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the product is not found.</exception>
    public async Task HandleAsync(UpdateProductCommand cmd, CancellationToken ct)
    {
        var product = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Product {cmd.Id} not found.");

        var prices = await ProductPriceInputs.ResolveAsync(cmd.Prices, cmd.MonthlyPrice, cmd.AnnualPrice, payerCurrency, ct);
        prices.ApplyTo(product);

        // The legacy single-currency columns are kept one release so a rollback and the admin
        // services grid still read real numbers: they mirror the base-currency prices as stored
        // after this save, so a legacy-form save that touched only one cycle is reflected too.
        product.Update(
            cmd.Name, cmd.Description, cmd.Website, cmd.Slug, cmd.PackageName,
            product.PriceFor(prices.BaseCurrencyCode, BillingCycle.Monthly) ?? NoPrice,
            product.PriceFor(prices.BaseCurrencyCode, BillingCycle.Annual) ?? NoPrice,
            cmd.ServerGroupId);

        await uow.SaveChangesAsync(ct);
    }
}
