using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Application.Support.Common;
using Innovayse.Hostpanel.Domain.Support;

namespace Innovayse.Hostpanel.Application.Support.Extensions;

/// <summary>Reads knowledge base articles with their category and translations, and shapes the responses.</summary>
public static class KbArticleMappingExtensions
{
    /// <summary>Reads articles with their category and every translation in full.</summary>
    /// <param name="articles">The articles to read.</param>
    /// <param name="categories">The categories to join them with.</param>
    /// <returns>One row per article, the translations carrying their whole body.</returns>
    public static IQueryable<KbArticleRow> SelectRows(this IQueryable<KbArticle> articles, IQueryable<KbCategory> categories) =>
        Rows(articles, categories, fullBody: true);

    /// <summary>Reads articles for a list: every translation, each body cut to the first characters an excerpt needs.</summary>
    /// <param name="articles">The articles to read.</param>
    /// <param name="categories">The categories to join them with.</param>
    /// <returns>One row per article, the translations carrying only the start of their body.</returns>
    public static IQueryable<KbArticleRow> SelectLeadRows(this IQueryable<KbArticle> articles, IQueryable<KbCategory> categories) =>
        Rows(articles, categories, fullBody: false);

    /// <summary>Shapes a row for the admin: every translation, keyed by locale.</summary>
    /// <param name="row">The article row.</param>
    /// <returns>The admin view of the article.</returns>
    public static KbArticleDto ToAdminDto(this KbArticleRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new KbArticleDto(
            row.Id,
            row.Slug,
            row.CategoryId,
            row.CategoryName,
            row.IsPublished,
            row.Translations.ToDictionary(t => t.Locale, t => new KbArticleTranslationDto(t.Title, t.Content)));
    }

    /// <summary>Shapes a row for a reader: the translation in the reader's language, else the fallback, else the first.</summary>
    /// <param name="row">The article row, read with full bodies.</param>
    /// <param name="requested">The language the reader asked for.</param>
    /// <param name="fallback">The language to fall back on.</param>
    /// <returns>The reader's view of the article, or null when it has no translation at all.</returns>
    public static PublishedKbArticleDto? ToPublishedDto(this KbArticleRow row, string requested, string fallback)
    {
        ArgumentNullException.ThrowIfNull(row);

        var text = row.Translations.ForLocale(t => t.Locale, requested, fallback);

        return text is null
            ? null
            : new PublishedKbArticleDto(row.Id, text.Title, text.Content, row.CategoryName, row.Slug, row.CategorySlug);
    }

    /// <summary>Shapes a row for a list: the title in the reader's language with a plain text excerpt of the body.</summary>
    /// <param name="row">The article row, read with body leads.</param>
    /// <param name="requested">The language the reader asked for.</param>
    /// <param name="fallback">The language to fall back on.</param>
    /// <returns>The list item, or null when the article has no translation at all.</returns>
    public static KbArticleSummaryDto? ToSummaryDto(this KbArticleRow row, string requested, string fallback)
    {
        ArgumentNullException.ThrowIfNull(row);

        var text = row.Translations.ForLocale(t => t.Locale, requested, fallback);

        return text is null
            ? null
            : new KbArticleSummaryDto(row.Id, text.Title, KbArticleExcerpt.From(text.Content), row.CategoryName, row.Slug, row.CategorySlug);
    }

    /// <summary>Converts the translations a command carries into the domain's input.</summary>
    /// <param name="translations">The translations as sent, keyed by locale code.</param>
    /// <returns>The same text as <see cref="KbArticleTranslationContent"/>.</returns>
    public static IReadOnlyDictionary<string, KbArticleTranslationContent> ToContent(
        this IReadOnlyDictionary<string, KbArticleTranslationDto> translations)
    {
        ArgumentNullException.ThrowIfNull(translations);

        return translations.ToDictionary(t => t.Key, t => new KbArticleTranslationContent(t.Value.Title, t.Value.Content));
    }

    /// <summary>The join of an article with its category and translations that both readers above share.</summary>
    /// <param name="articles">The articles to read.</param>
    /// <param name="categories">The categories to join them with.</param>
    /// <param name="fullBody">Whether each translation carries its whole body, or only the start an excerpt needs.</param>
    /// <returns>One row per article.</returns>
    private static IQueryable<KbArticleRow> Rows(IQueryable<KbArticle> articles, IQueryable<KbCategory> categories, bool fullBody)
    {
        ArgumentNullException.ThrowIfNull(articles);
        ArgumentNullException.ThrowIfNull(categories);

        return from a in articles
               join c in categories on a.CategoryId equals c.Id
               select new KbArticleRow(
                   a.Id,
                   a.Slug,
                   a.IsPublished,
                   c.Id,
                   c.Name,
                   c.Slug,
                   a.Translations
                       .Select(t => new KbTranslationRow(
                           t.Locale, t.Title, fullBody ? t.Content : t.Content.Substring(0, KbArticleExcerpt.SourceLength)))
                       .ToList());
    }
}
