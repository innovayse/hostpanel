using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Support.Common;
using Innovayse.Hostpanel.Application.Support.Extensions;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Support.Queries.ListKbArticles;

/// <summary>Returns a page of the knowledge base articles, published or not, with all their translations, narrowed by the search text and the filters.</summary>
public sealed class ListKbArticlesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ListKbArticlesHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The search, the filters and the paging window.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the page of articles, oldest first. This use case answers no refusal code.</returns>
    public async Task<Result<PaginatedList<KbArticleDto>>> HandleAsync(ListKbArticlesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var articles = _context.KbArticles.AsNoTracking().MatchingText(query.Search);

        if (query.CategoryId is { } categoryId)
        {
            articles = articles.Where(a => a.CategoryId == categoryId);
        }

        if (query.IsPublished is { } isPublished)
        {
            articles = articles.Where(a => a.IsPublished == isPublished);
        }

        var ordered = articles.OrderBy(a => a.Id);
        var totalCount = await ordered.CountAsync(ct);
        var rows = await ordered
            .Paginate(query.Page, query.PageSize)
            .SelectRows(_context.KbCategories.AsNoTracking())
            .ToListAsync(ct);

        var items = rows.Select(r => r.ToAdminDto()).ToList();
        return Result.Success(new PaginatedList<KbArticleDto>(items, totalCount, query.Page, query.PageSize));
    }
}
