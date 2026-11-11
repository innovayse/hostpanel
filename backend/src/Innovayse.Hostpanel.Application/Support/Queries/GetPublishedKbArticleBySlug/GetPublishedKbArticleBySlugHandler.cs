using System.Globalization;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Application.Support.Common;
using Innovayse.Hostpanel.Application.Support.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Application.Support.Queries.GetPublishedKbArticleBySlug;

/// <summary>Loads one published knowledge base article by slug, in the reader's language.</summary>
public sealed class GetPublishedKbArticleBySlugHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The locale settings of this deployment.</summary>
    private readonly IOptions<LocaleOptions> _localeOptions;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="localeOptions">The locale settings of this deployment.</param>
    public GetPublishedKbArticleBySlugHandler(IHostpanelDbContext context, IOptions<LocaleOptions> localeOptions)
    {
        _context = context;
        _localeOptions = localeOptions;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The slug to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The article in the reader's language (else the fallback, else the first language it has), or
    /// <c>KBARTICLE_NOT_FOUND</c> when no published article has the slug. A purely numeric slug names no article,
    /// because none can be stored.
    /// </returns>
    public async Task<Result<PublishedKbArticleDto>> HandleAsync(GetPublishedKbArticleBySlugQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var slug = query.Slug.ToLower(CultureInfo.InvariantCulture);
        var row = await _context.KbArticles
            .AsNoTracking()
            .Where(a => a.Slug == slug && a.IsPublished)
            .SelectRows(_context.KbCategories.AsNoTracking())
            .FirstOrDefaultAsync(ct);

        var article = row?.ToPublishedDto(KbLanguage.Requested(), KbLanguage.Fallback(_localeOptions.Value));

        return article is null
            ? Result<PublishedKbArticleDto>.FailureCode(HostpanelErrorCodes.KBARTICLE_NOT_FOUND)
            : Result<PublishedKbArticleDto>.Success(article);
    }
}
