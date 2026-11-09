using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Support.Common;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Support.Queries.ListPublishedKbCategories;

/// <summary>
/// Returns the categories that hold published knowledge base articles with their counts, grouped and counted in
/// the database so no article body is read.
/// </summary>
public sealed class ListPublishedKbCategoriesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ListPublishedKbCategoriesHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The query; it carries no input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with one entry per category that holds a published article, ordered by name, with its slug and
    /// the number of published articles. A category holding only drafts does not appear. This use case answers
    /// no refusal code.
    /// </returns>
    public async Task<Result<IReadOnlyList<PublishedKbCategoryDto>>> HandleAsync(
        ListPublishedKbCategoriesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var categories = await (
                from a in _context.KbArticles.AsNoTracking()
                where a.IsPublished
                join c in _context.KbCategories.AsNoTracking() on a.CategoryId equals c.Id
                group a by new { c.Id, c.Name, c.Slug } into g
                orderby g.Key.Name.ToLower(), g.Key.Id
                select new PublishedKbCategoryDto(g.Key.Name, g.Count(), g.Key.Slug))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<PublishedKbCategoryDto>>(categories);
    }
}
