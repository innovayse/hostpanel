namespace Innovayse.Application.Support.Commands.CreateKbCategory;

using Innovayse.Application.Common;
using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Creates a new knowledge base category.
/// </summary>
public sealed class CreateKbCategoryHandler(IKbCategoryRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="CreateKbCategoryCommand"/>.
    /// </summary>
    /// <param name="command">The create category command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ID of the newly created category.</returns>
    public async Task<int> HandleAsync(CreateKbCategoryCommand command, CancellationToken ct)
    {
        var category = KbCategory.Create(command.Name, command.Description, command.IsHidden, command.ParentCategoryId);
        repo.Add(category);
        await uow.SaveChangesAsync(ct);
        return category.Id;
    }
}
