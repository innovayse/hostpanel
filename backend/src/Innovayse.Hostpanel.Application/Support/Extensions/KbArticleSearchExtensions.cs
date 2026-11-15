using Innovayse.Hostpanel.Application.Common.Extensions;
using Innovayse.Hostpanel.Domain.Support;

namespace Innovayse.Hostpanel.Application.Support.Extensions;

/// <summary>
/// The one way a knowledge base list applies the text a person typed into its search box, so the staff list and the
/// published list find the same articles for the same words.
/// </summary>
public static class KbArticleSearchExtensions
{
    /// <summary>
    /// Keeps the articles whose title or body, in any language they are written in, contains the search text in any letter
    /// case. An article that matches in several languages is listed once.
    /// </summary>
    /// <param name="articles">The articles to narrow.</param>
    /// <param name="term">What the person typed; blank means no search.</param>
    /// <returns>The narrowed articles, or <paramref name="articles"/> when the term is blank.</returns>
    public static IQueryable<KbArticle> MatchingText(this IQueryable<KbArticle> articles, string? term)
    {
        ArgumentNullException.ThrowIfNull(articles);

        return articles.SearchChildren(term, a => a.Translations, t => t.Title, t => t.Content);
    }
}
