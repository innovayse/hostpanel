namespace Innovayse.Application.Support.Commands.CreateDownloadCategory;

using Innovayse.Application.Common;
using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Creates a new download category.
/// </summary>
public sealed class CreateDownloadCategoryHandler(IDownloadRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Handles <see cref="CreateDownloadCategoryCommand"/>.
    /// </summary>
    /// <param name="command">The create category command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ID of the newly created category.</returns>
    public async Task<int> HandleAsync(CreateDownloadCategoryCommand command, CancellationToken ct)
    {
        var category = DownloadCategory.Create(
            command.Name,
            command.Description,
            command.IsHidden,
            command.ParentCategoryId);

        repo.AddCategory(category);
        await uow.SaveChangesAsync(ct);
        return category.Id;
    }
}
