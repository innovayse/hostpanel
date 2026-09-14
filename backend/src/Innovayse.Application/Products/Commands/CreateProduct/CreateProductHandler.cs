namespace Innovayse.Application.Products.Commands.CreateProduct;

using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Products.Common;
using Innovayse.Application.Products.Extensions;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;

/// <summary>Creates a new product and persists it.</summary>
/// <param name="repo">Product repository the new product is added to.</param>
/// <param name="groupRepo">Product group repository, to check the parent group exists.</param>
/// <param name="uow">Unit of work for persistence.</param>
/// <param name="payerCurrency">Resolves the base currency the legacy price fields and columns are in.</param>
public sealed class CreateProductHandler(
    IProductRepository repo,
    IProductGroupRepository groupRepo,
    IUnitOfWork uow,
    IPayerCurrencyResolver payerCurrency)
{
    /// <summary>
    /// Handles <see cref="CreateProductCommand"/>.
    /// </summary>
    /// <param name="cmd">The create command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created product ID.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the product group is not found.</exception>
    public async Task<int> HandleAsync(CreateProductCommand cmd, CancellationToken ct)
    {
        _ = await groupRepo.FindByIdAsync(cmd.GroupId, ct)
            ?? throw new InvalidOperationException($"Product group {cmd.GroupId} not found.");

        var prices = await ProductPriceInputs.ResolveAsync(cmd.Prices, cmd.MonthlyPrice, cmd.AnnualPrice, payerCurrency, ct);

        // The legacy single-currency columns are kept one release so a rollback and the admin
        // services grid still read real numbers: they mirror the base-currency prices.
        var product = Product.Create(
            cmd.GroupId, cmd.Name, cmd.Description, cmd.Website, cmd.Slug, cmd.PackageName, cmd.Type,
            prices.BaseMonthly, prices.BaseAnnual, cmd.ServerGroupId);
        prices.ApplyTo(product);

        repo.Add(product);
        await uow.SaveChangesAsync(ct);
        return product.Id;
    }
}
