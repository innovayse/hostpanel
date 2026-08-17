namespace Innovayse.Application.Products.Commands.UpdateProductFeature;

using Innovayse.Application.Common;
using Innovayse.Domain.Products.Interfaces;

/// <summary>Updates an existing specification line.</summary>
public sealed class UpdateProductFeatureHandler(IProductFeatureRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="UpdateProductFeatureCommand"/>.
    /// </summary>
    /// <param name="cmd">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the line is not found.</exception>
    public async Task HandleAsync(UpdateProductFeatureCommand cmd, CancellationToken ct)
    {
        var feature = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Product feature {cmd.Id} not found.");

        // Renaming a line onto a label the product already uses would hide one of them
        // from the comparison table, which keys its rows on the label. This line itself
        // is excluded, so saving a line without renaming it is never blocked.
        if (await repo.ExistsWithLabelAsync(feature.ProductId, cmd.Label, cmd.Id, ct))
        {
            throw new InvalidOperationException(
                $"This product already has a \"{cmd.Label.Trim()}\" line. Edit that one instead.");
        }

        feature.Update(cmd.Label, cmd.Value, cmd.SortOrder);
        await uow.SaveChangesAsync(ct);
    }
}
