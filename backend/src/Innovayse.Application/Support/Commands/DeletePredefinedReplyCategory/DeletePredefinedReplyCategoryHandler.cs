namespace Innovayse.Application.Support.Commands.DeletePredefinedReplyCategory;

using Innovayse.Application.Common;
using Innovayse.Domain.Support.Interfaces;

/// <summary>Handles <see cref="DeletePredefinedReplyCategoryCommand"/> by permanently removing the category.</summary>
public sealed class DeletePredefinedReplyCategoryHandler(IPredefinedReplyRepository repo, IUnitOfWork uow)
{
    /// <summary>
    /// Deletes a predefined reply category.
    /// </summary>
    /// <param name="cmd">The delete category command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">Thrown when the category is not found.</exception>
    public async Task HandleAsync(DeletePredefinedReplyCategoryCommand cmd, CancellationToken ct)
    {
        var category = await repo.FindCategoryByIdAsync(cmd.Id, ct)
            ?? throw new InvalidOperationException($"Predefined reply category {cmd.Id} not found.");

        repo.RemoveCategory(category);
        await uow.SaveChangesAsync(ct);
    }
}
