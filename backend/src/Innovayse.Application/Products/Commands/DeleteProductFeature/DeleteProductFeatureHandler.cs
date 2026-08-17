namespace Innovayse.Application.Products.Commands.DeleteProductFeature;

using Innovayse.Application.Common;
using Innovayse.Domain.Products.Interfaces;

/// <summary>Removes a specification line.</summary>
public sealed class DeleteProductFeatureHandler(IProductFeatureRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="DeleteProductFeatureCommand"/>.
    /// </summary>
    /// <param name="cmd">The delete command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the line is not found.</exception>
    public async Task HandleAsync(DeleteProductFeatureCommand cmd, CancellationToken ct)
    {
        var feature = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Product feature {cmd.Id} not found.");

        repo.Remove(feature);
        await uow.SaveChangesAsync(ct);
    }
}
