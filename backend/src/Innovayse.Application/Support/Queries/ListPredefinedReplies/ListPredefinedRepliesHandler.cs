namespace Innovayse.Application.Support.Queries.ListPredefinedReplies;

using Innovayse.Application.Support.DTOs;
using Innovayse.Domain.Support.Interfaces;

/// <summary>
/// Returns all predefined replies, optionally filtered by category.
/// Maps results to <see cref="PredefinedReplyDto"/>.
/// </summary>
public sealed class ListPredefinedRepliesHandler(IPredefinedReplyRepository repo)
{
    /// <summary>
    /// Handles <see cref="ListPredefinedRepliesQuery"/>.
    /// </summary>
    /// <param name="query">The list replies query with optional category filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of predefined reply DTOs.</returns>
    public async Task<IReadOnlyList<PredefinedReplyDto>> HandleAsync(
        ListPredefinedRepliesQuery query, CancellationToken ct)
    {
        var replies = await repo.ListAsync(query.CategoryId, ct);

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
