using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Support.Common;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Support.Queries.ListKbCategories;

/// <summary>Returns all knowledge base categories with article counts.</summary>
public sealed class ListKbCategoriesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ListKbCategoriesHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The list categories query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with every category. This use case answers no refusal code.</returns>
    public async Task<Result<IReadOnlyList<KbCategoryDto>>> HandleAsync(ListKbCategoriesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var categories = await _context.KbCategories.AsNoTracking().ToListAsync(ct);
        var countsByCategory = await _context.KbArticles
            .AsNoTracking()
            .GroupBy(a => a.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.CategoryId, g => g.Count, ct);

        var items = categories
            .Select(c => new KbCategoryDto(
                c.Id,
                c.Name,
                c.Description,
                c.IsHidden,
                c.ParentCategoryId,
                countsByCategory.GetValueOrDefault(c.Id, 0),
                c.Slug))
            .ToList();

        return Result.Success<IReadOnlyList<KbCategoryDto>>(items);
    }
}
