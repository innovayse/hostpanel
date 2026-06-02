namespace Innovayse.Application.Support.Queries.ListDownloadCategories;

using Innovayse.Application.Support.DTOs;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Returns all download categories with download counts per category.
/// </summary>
public sealed class ListDownloadCategoriesHandler(IDownloadRepository repo)
{
    /// <summary>
    /// Handles <see cref="ListDownloadCategoriesQuery"/>.
    /// </summary>
    /// <param name="query">The list categories query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of category DTOs with download counts.</returns>
    public async Task<IReadOnlyList<DownloadCategoryDto>> HandleAsync(
        ListDownloadCategoriesQuery query, CancellationToken ct)
    {
        var categories = await repo.ListCategoriesAsync(ct);
        var result = new List<DownloadCategoryDto>(categories.Count);

        foreach (var c in categories)
        {
            var count = await repo.CountByCategoryIdAsync(c.Id, ct);
            result.Add(new DownloadCategoryDto(
                c.Id,
                c.Name,
                c.Description,
                c.IsHidden,
                c.ParentCategoryId,
                count));
        }

        return result;
    }
}
