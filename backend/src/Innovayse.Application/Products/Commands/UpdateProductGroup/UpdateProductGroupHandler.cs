namespace Innovayse.Application.Products.Commands.UpdateProductGroup;

using Innovayse.Application.Common;
using Innovayse.Domain.Products.Interfaces;

/// <summary>Updates an existing product group.</summary>
public sealed class UpdateProductGroupHandler(
    IProductGroupRepository repo,
    IUnitOfWork uow)
{
    /// <summary>
    /// Handles the <see cref="UpdateProductGroupCommand"/>.
    /// </summary>
    /// <param name="cmd">The update command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the product group is not found.</exception>
    public async Task HandleAsync(UpdateProductGroupCommand cmd, CancellationToken ct)
    {
        var group = await repo.FindByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Product group {cmd.Id} not found.");

        group.Update(cmd.Name, cmd.Description);
        await uow.SaveChangesAsync(ct);
    }
}
