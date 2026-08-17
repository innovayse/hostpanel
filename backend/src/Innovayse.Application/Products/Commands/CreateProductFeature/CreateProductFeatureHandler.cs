namespace Innovayse.Application.Products.Commands.CreateProductFeature;

using Innovayse.Application.Common;
using Innovayse.Domain.Products;
using Innovayse.Domain.Products.Interfaces;

/// <summary>Adds a specification line to a product and persists it.</summary>
public sealed class CreateProductFeatureHandler(
    IProductFeatureRepository repo,
    IProductRepository products,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="CreateProductFeatureCommand"/>.
    /// </summary>
    /// <param name="cmd">The create command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created feature line ID.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the product is not found.</exception>
    public async Task<int> HandleAsync(CreateProductFeatureCommand cmd, CancellationToken ct)
    {
        _ = await products.FindByIdAsync(cmd.ProductId, ct)
            ?? throw new InvalidOperationException($"Product {cmd.ProductId} not found.");

        var feature = ProductFeature.Create(cmd.ProductId, cmd.Label, cmd.Value, cmd.SortOrder);

        repo.Add(feature);
        await uow.SaveChangesAsync(ct);
        return feature.Id;
    }
}
