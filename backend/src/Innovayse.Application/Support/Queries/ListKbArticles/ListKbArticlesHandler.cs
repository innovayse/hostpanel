namespace Innovayse.Application.Support.Queries.ListKbArticles;

using Innovayse.Application.Support.DTOs;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Returns knowledge base articles mapped to <see cref="KbArticleDto"/>.
/// </summary>
public sealed class ListKbArticlesHandler(IKbArticleRepository repo)
{
    /// <summary>
    /// Handles <see cref="ListKbArticlesQuery"/>.
    /// </summary>
    /// <param name="query">The list KB articles query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of KB article DTOs.</returns>
    public async Task<IReadOnlyList<KbArticleDto>> HandleAsync(ListKbArticlesQuery query, CancellationToken ct)
    {
        var articles = query.PublishedOnly
            ? await repo.ListPublishedAsync(ct)
            : await repo.ListAllAsync(ct);

        return articles
            .Select(a => new KbArticleDto(a.Id, a.Title, a.Content, a.Category, a.IsPublished))
            .ToList();
    }
}
