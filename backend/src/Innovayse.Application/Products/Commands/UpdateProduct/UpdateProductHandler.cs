namespace Innovayse.Application.Products.Commands.UpdateProduct;

using Innovayse.Application.Common;
using Innovayse.Domain.Products.Interfaces;

/// <summary>Updates an existing product's details and prices.</summary>
public sealed class UpdateProductHandler(IProductRepository repo, IUnitOfWork uow)
{
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

        product.Update(cmd.Name, cmd.Description, cmd.Website, cmd.MonthlyPrice, cmd.AnnualPrice);
        await uow.SaveChangesAsync(ct);
    }
}
