namespace Innovayse.Application.Support.Queries.SearchPredefinedReplies;

using Innovayse.Application.Support.DTOs;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Searches predefined replies by name or content and maps results to <see cref="PredefinedReplyDto"/>.
/// </summary>
public sealed class SearchPredefinedRepliesHandler(IPredefinedReplyRepository repo)
{
    /// <summary>
    /// Handles <see cref="SearchPredefinedRepliesQuery"/>.
    /// </summary>
    /// <param name="query">The search query containing the search term.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of matching predefined reply DTOs.</returns>
    public async Task<IReadOnlyList<PredefinedReplyDto>> HandleAsync(
        SearchPredefinedRepliesQuery query, CancellationToken ct)
    {
        var replies = await repo.SearchAsync(query.SearchTerm, ct);

        return replies
            .Select(x => new PredefinedReplyDto(
                x.Reply.Id,
                x.Reply.Name,
                x.Reply.Content,
                x.Reply.CategoryId,
                x.CategoryName))
            .ToList();
    }
}
