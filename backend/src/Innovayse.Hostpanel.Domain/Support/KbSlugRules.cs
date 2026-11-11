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
/// <para>
/// The shape is written for two regular expression engines. .NET's <c>$</c> also matches before a trailing newline,
/// so the .NET patterns anchor with <c>\A</c> and <c>\z</c>, which match only at the very start and the very end.
/// PostgreSQL's <c>$</c> matches only at the end of the text, so the patterns of the check constraints keep
/// <c>^</c> and <c>$</c>. Both refuse <c>"foo\n"</c>; a test pins that for the domain and for the database.
/// </para>
/// </remarks>
public static class KbSlugRules
{
    /// <summary>Longest a slug may be: matches the <c>Slug</c> columns.</summary>
    public const int MaxLength = 150;

    /// <summary>The shape every slug has, as a .NET regular expression (no trailing newline is allowed).</summary>
    public const string Pattern = @"\A[a-z0-9]+(-[a-z0-9]+)*\z";

    /// <summary>The only shape a purely numeric string has, as a .NET regular expression: such a slug is refused.</summary>
    public const string NumericPattern = @"\A[0-9]+\z";

    /// <summary>The same shape as <see cref="Pattern"/>, as the PostgreSQL regular expression of the check constraints.</summary>
    public const string DatabasePattern = "^[a-z0-9]+(-[a-z0-9]+)*$";

    /// <summary>The same shape as <see cref="NumericPattern"/>, as the PostgreSQL regular expression of the check constraints.</summary>
    public const string DatabaseNumericPattern = "^[0-9]+$";

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

    /// <summary>Refuses a text that cannot be the slug of a category.</summary>
    /// <param name="slug">The slug to check.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="slug"/> is not a valid category slug.</exception>
    public static void EnsureValidCategorySlug(string? slug)
    {
        if (!IsValidCategorySlug(slug))
        {
            throw new ArgumentException($"'{slug}' is not a valid category slug.", nameof(slug));
        }
    }

    /// <summary>Refuses a text that cannot be the slug of an article.</summary>
    /// <param name="slug">The slug to check.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="slug"/> is not a valid article slug.</exception>
    public static void EnsureValidArticleSlug(string? slug)
    {
        if (!IsValidArticleSlug(slug))
        {
            throw new ArgumentException($"'{slug}' is not a valid article slug.", nameof(slug));
        }
    }
}
