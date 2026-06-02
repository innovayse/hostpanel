namespace Innovayse.Application.Billing.Queries.ListClientBillableItems;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Returns uninvoiced items (all) and invoiced items (paginated) for a client.
/// </summary>
public sealed class ListClientBillableItemsHandler(IBillableItemRepository repo)
{
    /// <summary>
    /// Handles <see cref="ListClientBillableItemsQuery"/>.
    /// </summary>
    /// <param name="qry">The list billable items query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing uninvoiced items, their total, and paginated invoiced items.</returns>
    public async Task<ClientBillableItemsResultDto> HandleAsync(ListClientBillableItemsQuery qry, CancellationToken ct)
    {
        var uninvoiced = await repo.ListUninvoicedAsync(qry.ClientId, ct);
        var uninvoicedDtos = uninvoiced.Select(MapToDto).ToList();
        var uninvoicedTotal = uninvoiced.Sum(x => x.Amount);

        var (invoicedItems, totalCount) = await repo.ListInvoicedByClientAsync(
            qry.ClientId, qry.InvoicedPage, qry.InvoicedPageSize, ct);
        var invoicedDtos = invoicedItems.Select(MapToDto).ToList();

        var pagedInvoiced = new PagedResult<ClientBillableItemDto>(
            invoicedDtos, totalCount, qry.InvoicedPage, qry.InvoicedPageSize);

        return new ClientBillableItemsResultDto(uninvoicedDtos, uninvoicedTotal, pagedInvoiced);
    }

    /// <summary>Maps a domain billable item to its DTO representation.</summary>
    /// <param name="item">The domain entity.</param>
    /// <returns>The DTO.</returns>
    private static ClientBillableItemDto MapToDto(BillableItem item) =>
        new(
            item.Id,
            item.ClientId,
            item.Description,
            item.Amount,
            item.Currency,
            item.Type.ToString(),
            item.RecurringPeriod,
            item.IsInvoiced,
            item.InvoiceId,
            item.NextDueDate,
            item.CreatedAt);
}
