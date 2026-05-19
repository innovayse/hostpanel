namespace Innovayse.Application.Billing.Queries.ListBillableItems;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;

/// <summary>
/// Returns uninvoiced items (all) and invoiced items (paginated) for a client.
/// </summary>
public sealed class ListBillableItemsHandler(IBillableItemRepository repo)
{
    /// <summary>
    /// Handles <see cref="ListBillableItemsQuery"/>.
    /// </summary>
    /// <param name="qry">The list billable items query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result containing uninvoiced items, their total, and paginated invoiced items.</returns>
    public async Task<BillableItemsResultDto> HandleAsync(ListBillableItemsQuery qry, CancellationToken ct)
    {
        var uninvoiced = await repo.ListUninvoicedByClientAsync(qry.ClientId, ct);
        var uninvoicedDtos = uninvoiced.Select(MapToDto).ToList();
        var uninvoicedTotal = uninvoiced.Sum(x => x.Amount);

        var (invoicedItems, totalCount) = await repo.ListInvoicedByClientAsync(
            qry.ClientId, qry.InvoicedPage, qry.InvoicedPageSize, ct);
        var invoicedDtos = invoicedItems.Select(MapToDto).ToList();

        var pagedInvoiced = new PagedResult<BillableItemDto>(
            invoicedDtos, totalCount, qry.InvoicedPage, qry.InvoicedPageSize);

        return new BillableItemsResultDto(uninvoicedDtos, uninvoicedTotal, pagedInvoiced);
    }

    /// <summary>Maps a domain billable item to its DTO representation.</summary>
    /// <param name="item">The domain entity.</param>
    /// <returns>The DTO.</returns>
    private static BillableItemDto MapToDto(BillableItem item) =>
        new(
            item.Id,
            item.ClientId,
            item.ServiceId,
            ServiceName: null,
            item.Description,
            item.Amount,
            item.HoursQty,
            item.IsHours,
            item.InvoiceAction.ToString(),
            item.DueDate,
            item.InvoiceId,
            item.InvoiceCount,
            item.RecurrenceInterval,
            item.RecurrencePeriod?.ToString(),
            item.RecurrenceLimit,
            item.CreatedAt);
}
