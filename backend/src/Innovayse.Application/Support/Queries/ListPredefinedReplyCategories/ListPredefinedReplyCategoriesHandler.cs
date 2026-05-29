namespace Innovayse.Application.Support.Queries.ListPredefinedReplyCategories;

using Innovayse.Application.Support.DTOs;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Returns all predefined reply categories with their reply counts.
/// Maps results to <see cref="PredefinedReplyCategoryDto"/>.
/// </summary>
public sealed class ListPredefinedReplyCategoriesHandler(IPredefinedReplyRepository repo)
{
    /// <summary>
    /// Handles <see cref="ListPredefinedReplyCategoriesQuery"/>.
    /// </summary>
    /// <param name="query">The list categories query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of all predefined reply category DTOs.</returns>
    public async Task<IReadOnlyList<PredefinedReplyCategoryDto>> HandleAsync(
        ListPredefinedReplyCategoriesQuery query, CancellationToken ct)
    {
        var categories = await repo.ListCategoriesAsync(ct);

        return categories
            .Select(x => new PredefinedReplyCategoryDto(
                x.Category.Id,
                x.Category.Name,
                x.Category.ParentCategoryId,
                x.ReplyCount))
            .ToList();
    }
}
