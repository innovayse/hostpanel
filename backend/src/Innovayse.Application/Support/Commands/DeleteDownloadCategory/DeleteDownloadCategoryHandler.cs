namespace Innovayse.Application.Support.Commands.DeleteDownloadCategory;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Deletes a download category.
/// </summary>
public sealed class DeleteDownloadCategoryHandler(IDownloadRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="DeleteDownloadCategoryCommand"/>.
    /// </summary>
    /// <param name="command">The delete category command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the category is not found.</exception>
    public async Task HandleAsync(DeleteDownloadCategoryCommand command, CancellationToken ct)
    {
        var category = await repo.FindCategoryByIdAsync(command.Id, ct)
            ?? throw new InvalidOperationException($"Download category {command.Id} not found.");

        repo.RemoveCategory(category);
        await uow.SaveChangesAsync(ct);
    }
}
