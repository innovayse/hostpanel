namespace Innovayse.Application.Support.Queries.GetKbArticle;

using Innovayse.Application.Support.DTOs;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Loads a single knowledge base article and maps it to <see cref="KbArticleDto"/>.
/// </summary>
public sealed class GetKbArticleHandler(IKbArticleRepository repo)
{
    /// <summary>
    /// Handles <see cref="GetKbArticleQuery"/>.
    /// </summary>
    /// <param name="query">The get KB article query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="KbArticleDto"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the article is not found.</exception>
    public async Task<KbArticleDto> HandleAsync(GetKbArticleQuery query, CancellationToken ct)
    {
        var article = await repo.FindByIdAsync(query.Id, ct)
            ?? throw new InvalidOperationException($"KbArticle {query.Id} not found.");

        return new KbArticleDto(article.Id, article.Title, article.Content, article.Category, article.IsPublished);
    }
}
