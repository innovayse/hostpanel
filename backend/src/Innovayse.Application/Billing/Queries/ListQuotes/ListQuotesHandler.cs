namespace Innovayse.Application.Billing.Queries.ListQuotes;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Clients.Interfaces;

/// <summary>Returns a paginated list of all quotes for admin consumption with client names.</summary>
public sealed class ListQuotesHandler(IQuoteRepository repo, IClientRepository clientRepo)
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

        // Batch-resolve client names
        var clientIds = items.Select(q => q.ClientId).Distinct();
        var clients = await clientRepo.FindByIdsAsync(clientIds, ct);
        var clientMap = clients.ToDictionary(c => c.Id, c => $"{c.FirstName} {c.LastName}");

        var dtos = items.Select(q => new QuoteListItemDto(
            q.Id,
            q.ClientId,
            clientMap.GetValueOrDefault(q.ClientId, "Unknown"),
            q.Subject,
            q.Status,
            q.ExpiryDate,
            q.Total,
            q.CreatedAt))
            .ToList();

        return new PagedResult<QuoteListItemDto>(dtos, total, page, pageSize);
    }
}
