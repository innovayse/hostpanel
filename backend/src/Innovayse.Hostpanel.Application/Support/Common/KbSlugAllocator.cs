using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Support;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Support.Common;

/// <summary>
/// Gives a new article or category a slug nobody else has: it takes the candidate <see cref="SlugGenerator"/> builds
/// and, while that is taken, tries the next numbered one, checking every value it tries against what is stored.
/// </summary>
/// <remarks>
/// Slugs are stored lower case (a check constraint says so), so comparing them ordinally is comparing them without
/// regard to case. Two requests allocating at the same moment can still pick the same free value; the unique index
/// then refuses the second save, and the use case allocates again.
/// </remarks>
public sealed class KbSlugAllocator
{
    /// <summary>The slug of an article whose title leaves nothing usable.</summary>
    public const string ArticleFallback = "article";

    /// <summary>The slug of a category whose name leaves nothing usable.</summary>
    public const string CategoryFallback = "category";

    /// <summary>Room kept free below the column limit for a numbered suffix such as <c>-123456</c>.</summary>
    private const int SuffixRoom = 10;

    /// <summary>The module's data context, to read the slugs already taken.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the allocator.</summary>
    /// <param name="context">The module's data context, to read the slugs already taken.</param>
    public KbSlugAllocator(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Allocates the slug of a new article.</summary>
    /// <param name="title">The article's title, in the language the slug is built from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A valid article slug no stored article has and the article routes do not reserve.</returns>
    public async Task<string> AllocateForArticleAsync(string title, CancellationToken ct)
    {
        var candidate = Candidate(title, ArticleFallback);
        var taken = await _context.KbArticles
            .AsNoTracking()
            .Where(a => a.Slug.StartsWith(candidate))
            .Select(a => a.Slug)
            .ToListAsync(ct);

        return FirstFree(candidate, taken.Concat(KbSlugRules.ReservedArticleSlugs).ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>Allocates the slug of a new category.</summary>
    /// <param name="name">The category's name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A valid category slug no stored category has.</returns>
    public async Task<string> AllocateForCategoryAsync(string name, CancellationToken ct)
    {
        var candidate = Candidate(name, CategoryFallback);
        var taken = await _context.KbCategories
            .AsNoTracking()
            .Where(c => c.Slug.StartsWith(candidate))
            .Select(c => c.Slug)
            .ToListAsync(ct);

        return FirstFree(candidate, taken.ToHashSet(StringComparer.Ordinal));
    }

    /// <summary>Builds the candidate, short enough that a numbered suffix still fits the column.</summary>
    /// <param name="text">The title or name.</param>
    /// <param name="fallback">The slug used when the text leaves nothing usable.</param>
    /// <returns>The slug candidate.</returns>
    private static string Candidate(string text, string fallback) =>
        SlugGenerator.FromText(text, fallback, KbSlugRules.MaxLength - SuffixRoom);

    /// <summary>Returns the candidate, or the first numbered variant of it, that is not taken.</summary>
    /// <param name="candidate">The candidate.</param>
    /// <param name="taken">Every value that may not be given out, including the ones this method tried.</param>
    /// <returns>The first of <c>candidate</c>, <c>candidate-2</c>, <c>candidate-3</c>, ... not in <paramref name="taken"/>.</returns>
    private static string FirstFree(string candidate, HashSet<string> taken)
    {
        var slug = candidate;
        for (var number = 2; taken.Contains(slug); number++)
        {
            slug = $"{candidate}-{number}";
        }

        return slug;
    }
}
