namespace Innovayse.Application.Support.Commands.DeleteKbCategory;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Deletes a knowledge base category.
/// </summary>
public sealed class DeleteKbCategoryHandler(IKbCategoryRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="DeleteKbCategoryCommand"/>.
    /// </summary>
    /// <param name="command">The delete category command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the category is not found.</exception>
    public async Task HandleAsync(DeleteKbCategoryCommand command, CancellationToken ct)
    {
        var category = await repo.FindByIdAsync(command.Id, ct)
            ?? throw new InvalidOperationException($"KB category {command.Id} not found.");

        repo.Remove(category);
        await uow.SaveChangesAsync(ct);
    }
}
