using System.Globalization;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Support.Common;
using Innovayse.Hostpanel.Application.Support.Extensions;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Application.Support.Queries.ListPublishedKbArticles;

/// <summary>
/// Returns one page of published knowledge base articles in the reader's language, filtered by category and search
/// term in SQL; only the opening of each body is read, for the excerpt.
/// </summary>
public sealed class ListPublishedKbArticlesHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The locale settings of this deployment.</summary>
    private readonly IOptions<LocaleOptions> _localeOptions;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="localeOptions">The locale settings of this deployment.</param>
    public ListPublishedKbArticlesHandler(IHostpanelDbContext context, IOptions<LocaleOptions> localeOptions)
    {
        _context = context;
        _localeOptions = localeOptions;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The category (name or slug) and search filters, and the page asked for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with the page of published articles, oldest first, each in the reader's language (else the
    /// fallback, else the first language it has). The category filter matches a category name without regard to
    /// case, or its slug (a slug wins when a name equals another category's slug); the search term matches the title or
    /// body of any language. A hidden category (<c>KbCategory.IsHidden</c>) is not left out: the public feeds never looked at
    /// that flag and still do not. This use case answers no refusal code.
    /// </returns>
    public async Task<Result<PaginatedList<KbArticleSummaryDto>>> HandleAsync(ListPublishedKbArticlesQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        // An article with no translation at all has nothing to show: it is left out here, before the page is cut,
        // so a page never comes back shorter than asked for. (Every article has English, so this is a guard.)
        var articles = _context.KbArticles
            .AsNoTracking()
            .Where(a => a.IsPublished && a.Translations.Any());

        // The category is given as a slug or as a name. A slug wins: it is exact and never changes, so when one
        // category's name equals another category's slug, the text means the category that has it as its slug.
        var category = query.Category?.Trim().ToLower(CultureInfo.InvariantCulture);
        if (!string.IsNullOrEmpty(category))
        {
            var inCategory = _context.KbCategories
                .AsNoTracking()
                .Where(c => c.Slug == category
                    || (c.Name.ToLower() == category && !_context.KbCategories.Any(other => other.Slug == category)))
                .Select(c => c.Id);
            articles = articles.Where(a => inCategory.Contains(a.CategoryId));
        }

        // EF.Functions.Like rather than the Npgsql-only ILike: Application does not reference the
        // Npgsql provider package, and lower-casing both sides gives the case-insensitive match.
        // The term is escaped so a typed %, _ or backslash matches itself instead of widening the search.
        var search = query.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var term = LikePattern.Contains(search);
            articles = articles.Where(a => a.Translations.Any(t =>
                EF.Functions.Like(t.Title.ToLower(), term, LikePattern.EscapeCharacter)
                || EF.Functions.Like(t.Content.ToLower(), term, LikePattern.EscapeCharacter)));
        }

        var totalCount = await articles.CountAsync(ct);
        var rows = await articles
            .OrderBy(a => a.Id)
            .Paginate(query.Page, query.PageSize)
            .SelectLeadRows(_context.KbCategories.AsNoTracking())
            .ToListAsync(ct);

        var requested = KbLanguage.Requested();
        var fallback = KbLanguage.Fallback(_localeOptions.Value);
        var items = rows
            .Select(r => r.ToSummaryDto(requested, fallback))
            .OfType<KbArticleSummaryDto>()
            .ToList();

        return Result.Success(new PaginatedList<KbArticleSummaryDto>(items, totalCount, query.Page, query.PageSize));
    }
}
