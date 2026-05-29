namespace Innovayse.Application.Billing.Queries.ListQuotes;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Returns a paginated list of all quotes for admin consumption.</summary>
public sealed class ListQuotesHandler(IQuoteRepository repo)
{
    /// <summary>
    /// Handles <see cref="ListQuotesQuery"/>.
    /// </summary>
    /// <param name="query">The list quotes query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated result of quote list items.</returns>
    public async Task<PagedResult<QuoteListItemDto>> HandleAsync(ListQuotesQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (items, total) = await repo.ListAsync(page, pageSize, ct);

        var dtos = items.Select(q => new QuoteListItemDto(
            q.Id,
            q.ClientId,
            q.Subject,
            q.CreatedAt,
            q.ExpiryDate,
            q.Total,
            q.Status))
            .ToList();

        return new PagedResult<QuoteListItemDto>(dtos, total, page, pageSize);
    }
}
