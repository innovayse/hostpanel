namespace Innovayse.Hostpanel.Application.Support.Common;

/// <summary>The title and body of an article in one locale, as the admin sends and reads them.</summary>
/// <param name="Title">The article title in this locale.</param>
/// <param name="Content">The article body (Markdown or HTML) in this locale.</param>
public sealed record KbArticleTranslationDto(string Title, string Content);
