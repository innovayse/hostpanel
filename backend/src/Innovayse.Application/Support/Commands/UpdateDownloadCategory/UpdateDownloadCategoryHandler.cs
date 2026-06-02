namespace Innovayse.Application.Support.Commands.UpdateDownloadCategory;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Updates an existing download category.
/// </summary>
public sealed class UpdateDownloadCategoryHandler(IDownloadRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="UpdateDownloadCategoryCommand"/>.
    /// </summary>
    /// <param name="command">The update category command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the category is not found.</exception>
    public async Task HandleAsync(UpdateDownloadCategoryCommand command, CancellationToken ct)
    {
        var category = await repo.FindCategoryByIdAsync(command.Id, ct)
            ?? throw new InvalidOperationException($"Download category {command.Id} not found.");

        category.Update(command.Name, command.Description, command.IsHidden, command.ParentCategoryId);
        await uow.SaveChangesAsync(ct);
    }
}
