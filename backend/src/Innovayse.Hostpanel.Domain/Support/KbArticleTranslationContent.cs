namespace Innovayse.Hostpanel.Domain.Support;

/// <summary>The text of one locale of an article, handed to <see cref="KbArticle.ReplaceTranslations"/>.</summary>
/// <param name="Title">The article title as shown to readers of this locale.</param>
/// <param name="Content">The article body (Markdown or HTML) shown to readers of this locale.</param>
public sealed record KbArticleTranslationContent(string Title, string Content);
