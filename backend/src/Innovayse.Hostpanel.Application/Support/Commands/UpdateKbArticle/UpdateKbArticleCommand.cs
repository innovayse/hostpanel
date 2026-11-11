using Innovayse.Hostpanel.Application.Support.Common;

namespace Innovayse.Hostpanel.Application.Support.Commands.UpdateKbArticle;

/// <summary>Command to update an existing knowledge base article. The slug never changes.</summary>
/// <param name="Id">The article primary key; nullable so the route id can overlay the body.</param>
/// <param name="CategoryId">The id of the category the article belongs to.</param>
/// <param name="Translations">
/// The title and body in each locale, keyed by locale code; a locale left out is removed and the default language is
/// required.
/// </param>
public sealed record UpdateKbArticleCommand(int? Id, int CategoryId, Dictionary<string, KbArticleTranslationDto> Translations);
