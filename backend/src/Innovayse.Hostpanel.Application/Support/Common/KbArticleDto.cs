namespace Innovayse.Hostpanel.Application.Support.Common;

/// <summary>A knowledge base article as the admin sees it: every translation, with the category it belongs to.</summary>
/// <param name="Id">The article id.</param>
/// <param name="Slug">The article's address segment; fixed once the article exists.</param>
/// <param name="CategoryId">The id of the category the article belongs to.</param>
/// <param name="CategoryName">The name of that category.</param>
/// <param name="IsPublished">Whether the article is visible to clients.</param>
/// <param name="Translations">The title and body in each locale, keyed by locale code.</param>
public sealed record KbArticleDto(
    int Id,
    string Slug,
    int CategoryId,
    string CategoryName,
    bool IsPublished,
    Dictionary<string, KbArticleTranslationDto> Translations);
