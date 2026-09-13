namespace Innovayse.Application.Products.Commands.CreateProduct;

using Innovayse.Application.Common;
using Innovayse.Application.Products.Extensions;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;

/// <summary>Creates a new product and persists it.</summary>
public sealed class CreateProductHandler(
    IProductRepository repo,
    IProductGroupRepository groupRepo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Value written to the legacy single-currency price columns. They are kept one release so a
    /// rollback has data to read and are no longer written meaningfully; the prices live in
    /// <see cref="Product.Prices"/>.
    /// </summary>
    private const decimal LegacyPrice = 0m;

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

        var product = Product.Create(
            cmd.GroupId, cmd.Name, cmd.Description, cmd.Website, cmd.Slug, cmd.PackageName, cmd.Type,
            LegacyPrice, LegacyPrice, cmd.ServerGroupId);
        cmd.Prices.ApplyTo(product);

        repo.Add(product);
        await uow.SaveChangesAsync(ct);
        return product.Id;
    }
}
