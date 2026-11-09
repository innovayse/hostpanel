using System.Text.RegularExpressions;

namespace Innovayse.Hostpanel.Domain.Support;

/// <summary>
/// What a knowledge base slug may look like: the rules the domain, the slug allocator and the database check
/// constraints (<c>KbArticles</c> and <c>KbCategories</c>) all share.
/// </summary>
/// <remarks>
/// A slug is lower case Latin letters and digits joined by single hyphens. It is never purely numeric, so it
/// cannot be mistaken for the numeric id in <c>GET /api/knowledgebase/{id}</c>, and an article slug is never one
/// of the words the same route family reserves (<see cref="ReservedArticleSlugs"/>).
/// </remarks>
public static class KbSlugRules
{
    /// <summary>Longest a slug may be: matches the <c>Slug</c> columns.</summary>
    public const int MaxLength = 150;

    /// <summary>The shape every slug has, as a regular expression the database check constraints repeat.</summary>
    public const string Pattern = "^[a-z0-9]+(-[a-z0-9]+)*$";

    /// <summary>The only shape a purely numeric string has: such a slug is refused.</summary>
    public const string NumericPattern = "^[0-9]+$";

    /// <summary>Words that are literal segments of the article routes, so an article may not be called by them.</summary>
    public static readonly IReadOnlyList<string> ReservedArticleSlugs = ["articles", "categories"];

    /// <summary>The shape check, compiled once.</summary>
    private static readonly Regex ShapeRegex = new(Pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>The numeric check, compiled once.</summary>
    private static readonly Regex NumericRegex = new(NumericPattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Whether a text can be the slug of a category.</summary>
    /// <param name="slug">The text to check.</param>
    /// <returns>True when it is a well formed, non numeric slug no longer than <see cref="MaxLength"/>.</returns>
    public static bool IsValidCategorySlug(string? slug) =>
        !string.IsNullOrEmpty(slug)
        && slug.Length <= MaxLength
        && ShapeRegex.IsMatch(slug)
        && !NumericRegex.IsMatch(slug);

    /// <summary>Whether a text can be the slug of an article.</summary>
    /// <param name="slug">The text to check.</param>
    /// <returns>True when it is a valid category slug that is not one of <see cref="ReservedArticleSlugs"/>.</returns>
    public static bool IsValidArticleSlug(string? slug) =>
        IsValidCategorySlug(slug) && !ReservedArticleSlugs.Contains(slug!, StringComparer.Ordinal);
}
