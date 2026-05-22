namespace Innovayse.Application.Billing.DTOs;

using Innovayse.Application.Common;

/// <summary>Combined result of uninvoiced and invoiced billable items for a client.</summary>
/// <param name="UninvoicedItems">All uninvoiced billable items for the client.</param>
/// <param name="UninvoicedTotal">Sum of all uninvoiced item amounts.</param>
/// <param name="InvoicedItems">Paginated list of invoiced billable items.</param>
public record BillableItemsResultDto(
    IReadOnlyList<BillableItemDto> UninvoicedItems,
    decimal UninvoicedTotal,
    PagedResult<BillableItemDto> InvoicedItems);
