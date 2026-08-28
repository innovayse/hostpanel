namespace Innovayse.Application.Support.Queries.GetPublishedKbArticle;

using Innovayse.Application.Support.DTOs;
using Innovayse.Application.Support.Queries.GetKbArticle;
using Innovayse.Domain.Support.Interfaces;
using Wolverine;

/// <summary>
/// Returns a knowledge base article only while it is published.
/// </summary>
/// <remarks>
/// The check lives here rather than at the endpoint so it travels with the message: nothing can
/// read an article through <see cref="GetPublishedKbArticleQuery"/> without it having run. Once
/// visibility is settled the projection is the read the admin route already performs, so this
/// dispatches <see cref="GetKbArticleQuery"/> rather than growing a second copy of the mapping
/// that could drift from it.
/// </remarks>
/// <param name="repo">Article persistence, used for the visibility check only.</param>
/// <param name="bus">Wolverine bus, used to reach the shared read once visibility is settled.</param>
public sealed class GetPublishedKbArticleHandler(IKbArticleRepository repo, IMessageBus bus)
{
    /// <summary>Handles <see cref="GetPublishedKbArticleQuery"/>.</summary>
    /// <param name="query">The query naming the article to read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The matching <see cref="KbArticleDto"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the article does not exist and when it exists but is unpublished -- with the
    /// same wording for both. Ids here are sequential, and answering "exists but is a draft"
    /// differently from "no such row" would let a visitor map the unpublished backlog by id.
    /// </exception>
    public async Task<KbArticleDto> HandleAsync(GetPublishedKbArticleQuery query, CancellationToken ct)
    {
        var article = await repo.FindByIdAsync(query.Id, ct);

        if (article is null || !article.IsPublished)
        {
            throw new InvalidOperationException($"KbArticle {query.Id} not found.");
        }

        return await bus.InvokeAsync<KbArticleDto>(new GetKbArticleQuery(query.Id), ct);
    }
}
