namespace Innovayse.Application.Support.Queries.ListKbCategories;

using Innovayse.Application.Support.DTOs;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Returns all knowledge base categories with article counts.
/// </summary>
public sealed class ListKbCategoriesHandler(IKbCategoryRepository categoryRepo, IKbArticleRepository articleRepo)
{
    /// <summary>
    /// Handles <see cref="ListKbCategoriesQuery"/>.
    /// </summary>
    /// <param name="query">The list categories query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of category DTOs with article counts.</returns>
    public async Task<IReadOnlyList<KbCategoryDto>> HandleAsync(ListKbCategoriesQuery query, CancellationToken ct)
    {
        var categories = await categoryRepo.ListAllAsync(ct);
        var articles = await articleRepo.ListAllAsync(ct);

        var countsByCategory = articles
            .GroupBy(a => a.Category)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        return categories
            .Select(c => new KbCategoryDto(
                c.Id,
                c.Name,
                c.Description,
                c.IsHidden,
                c.ParentCategoryId,
                countsByCategory.GetValueOrDefault(c.Name, 0)))
            .ToList();
    }
}
