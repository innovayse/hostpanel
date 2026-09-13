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
/// <param name="payerCurrency">Resolves the base currency the legacy price fields are in.</param>
public sealed class UpdateProductHandler(IProductRepository repo, IUnitOfWork uow, IPayerCurrencyResolver payerCurrency)
{
    /// <summary>
    /// Value written to the legacy single-currency price columns. They are kept one release so a
    /// rollback has data to read and are no longer written meaningfully; the prices live in
    /// <see cref="Product.Prices"/>.
    /// </summary>
    private const decimal LegacyPrice = 0m;

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

        product.Update(
            cmd.Name, cmd.Description, cmd.Website, cmd.Slug, cmd.PackageName,
            LegacyPrice, LegacyPrice, cmd.ServerGroupId);
        var prices = await ProductPriceInputs.ResolveAsync(cmd.Prices, cmd.MonthlyPrice, cmd.AnnualPrice, payerCurrency, ct);
        prices.ApplyTo(product);

        await uow.SaveChangesAsync(ct);
    }
}
