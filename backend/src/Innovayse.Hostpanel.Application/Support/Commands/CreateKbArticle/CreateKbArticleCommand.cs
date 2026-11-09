using Innovayse.Hostpanel.Application.Support.Common;

namespace Innovayse.Hostpanel.Application.Support.Commands.CreateKbArticle;

/// <summary>Command to create a new knowledge base article.</summary>
/// <param name="CategoryId">The id of the category the article belongs to.</param>
/// <param name="Translations">The title and body in each locale, keyed by locale code; the default language is required.</param>
/// <param name="IsPublished">Whether the article is visible to clients at once; omitted means a draft.</param>
public sealed record CreateKbArticleCommand(
    int CategoryId,
    Dictionary<string, KbArticleTranslationDto> Translations,
    bool IsPublished = false);
