namespace Innovayse.Application.Support.Commands.CreatePredefinedReplyCategory;

using Innovayse.Application.Common;
using Innovayse.Domain.Support;
using Innovayse.Domain.Support.Interfaces;

/// <summary>Handles <see cref="CreatePredefinedReplyCategoryCommand"/> by creating a new category.</summary>
public sealed class CreatePredefinedReplyCategoryHandler(IPredefinedReplyRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Creates a new predefined reply category and persists it.
    /// </summary>
    /// <param name="cmd">The create category command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The ID of the newly created category.</returns>
    public async Task<int> HandleAsync(CreatePredefinedReplyCategoryCommand cmd, CancellationToken ct)
    {
        var category = PredefinedReplyCategory.Create(cmd.Name, cmd.ParentCategoryId);

        repo.AddCategory(category);
        await uow.SaveChangesAsync(ct);

        return category.Id;
    }
}
