namespace Innovayse.Application.Support.Commands.UpdateKbCategory;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Updates an existing knowledge base category.
/// </summary>
public sealed class UpdateKbCategoryHandler(IKbCategoryRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="UpdateKbCategoryCommand"/>.
    /// </summary>
    /// <param name="command">The update category command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the category is not found.</exception>
    public async Task HandleAsync(UpdateKbCategoryCommand command, CancellationToken ct)
    {
        var category = await repo.FindByIdAsync(command.Id, ct)
            ?? throw new InvalidOperationException($"KB category {command.Id} not found.");

        category.Update(command.Name, command.Description, command.IsHidden);
        await uow.SaveChangesAsync(ct);
    }
}
