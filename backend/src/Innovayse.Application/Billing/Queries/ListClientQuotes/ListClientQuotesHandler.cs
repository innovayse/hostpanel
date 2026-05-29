namespace Innovayse.Application.Billing.Queries.ListClientQuotes;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>Returns a paginated list of quotes for a specific client.</summary>
public sealed class ListClientQuotesHandler(IQuoteRepository repo)
{
    /// <summary>
    /// Handles <see cref="ListClientQuotesQuery"/>.
    /// </summary>
    /// <param name="query">The list client quotes query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated result of quote list items for the client.</returns>
    public async Task<PagedResult<QuoteListItemDto>> HandleAsync(ListClientQuotesQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (items, total) = await repo.ListByClientAsync(query.ClientId, page, pageSize, ct);

        var dtos = items.Select(q => new QuoteListItemDto(
<<<<<<< HEAD
            q.Id, q.ClientId, q.Subject, q.CreatedAt, q.ExpiryDate, q.Total, q.Status))
=======
            q.Id, q.ClientId, q.Subject, q.DateCreated, q.ValidUntil, q.Total, q.Stage))
>>>>>>> origin/main
            .ToList();

        return new PagedResult<QuoteListItemDto>(dtos, total, page, pageSize);
    }
}
