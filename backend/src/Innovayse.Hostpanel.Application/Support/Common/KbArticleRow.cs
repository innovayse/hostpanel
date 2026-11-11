namespace Innovayse.Hostpanel.Application.Support.Common;

/// <summary>An article with its category and every translation, as the queries read it before shaping a response.</summary>
/// <param name="Id">The article id.</param>
/// <param name="Slug">The article slug.</param>
/// <param name="IsPublished">Whether the article is visible to clients.</param>
/// <param name="CategoryId">The id of the article's category.</param>
/// <param name="CategoryName">The name of the article's category.</param>
/// <param name="CategorySlug">The slug of the article's category.</param>
/// <param name="Translations">Every locale the article is written in.</param>
public sealed record KbArticleRow(
    int Id,
    string Slug,
    bool IsPublished,
    int CategoryId,
    string CategoryName,
    string CategorySlug,
    List<KbTranslationRow> Translations);
