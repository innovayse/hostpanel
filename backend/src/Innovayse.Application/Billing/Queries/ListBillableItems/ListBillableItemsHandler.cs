namespace Innovayse.Application.Billing.Queries.ListBillableItems;

using Innovayse.Application.Billing.DTOs;
using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
<<<<<<< HEAD
using Innovayse.Domain.Clients.Interfaces;

/// <summary>Returns a paginated list of billable items with client names.</summary>
public sealed class ListBillableItemsHandler(IBillableItemRepository repo, IClientRepository clientRepo)
=======

/// <summary>
/// Returns uninvoiced items (all) and invoiced items (paginated) for a client.
/// </summary>
public sealed class ListBillableItemsHandler(IBillableItemRepository repo)
>>>>>>> origin/main
{
    /// <summary>
    /// Handles <see cref="ListBillableItemsQuery"/>.
    /// </summary>
<<<<<<< HEAD
    /// <param name="query">The list billable items query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Paginated result of billable item DTOs.</returns>
    public async Task<PagedResult<BillableItemDto>> HandleAsync(ListBillableItemsQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (items, total) = await repo.ListAsync(page, pageSize, ct);

        // Filter by type if specified
        var filtered = items;
        if (!string.IsNullOrEmpty(query.Type))
        {
            if (query.Type == "Recurring")
                filtered = items.Where(x => x.Type == BillableItemType.Recurring).ToList();
            else if (query.Type == "OneTime")
                filtered = items.Where(x => x.Type == BillableItemType.OneTime).ToList();
        }

        // Batch-resolve client names
        var clientIds = filtered.Select(item => item.ClientId).Distinct();
        var clients = await clientRepo.FindByIdsAsync(clientIds, ct);
        var clientMap = clients.ToDictionary(c => c.Id, c => $"{c.FirstName} {c.LastName}");

        var dtos = filtered.Select(item => new BillableItemDto(
            item.Id,
            item.ClientId,
            clientMap.GetValueOrDefault(item.ClientId, "Unknown"),
            item.Description,
            item.Amount,
            item.Currency,
            item.Type.ToString(),
            item.RecurringPeriod,
            item.IsInvoiced,
            item.InvoiceId,
            item.NextDueDate,
            item.CreatedAt))
            .ToList();

        return new PagedResult<BillableItemDto>(dtos, total, page, pageSize);
    }
=======
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
>>>>>>> origin/main
}
