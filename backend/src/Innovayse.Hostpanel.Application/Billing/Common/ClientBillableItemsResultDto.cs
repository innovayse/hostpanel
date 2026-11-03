using Innovayse.Hostpanel.Application.Billing.Currencies.Common;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Billing.Common;
/// <summary>Combined result of uninvoiced and invoiced billable items for a client profile view.</summary>
/// <param name="UninvoicedItems">All uninvoiced billable items for the client.</param>
/// <param name="UninvoicedTotal">Sum of the uninvoiced item amounts, one entry per currency, ordered by currency code; empty when there are none. Amounts of different currencies are never added together.</param>
/// <param name="InvoicedItems">Paginated list of invoiced billable items.</param>
public sealed record ClientBillableItemsResultDto(
    IReadOnlyList<ClientBillableItemDto> UninvoicedItems,
    IReadOnlyList<CurrencyAmountDto> UninvoicedTotal,
    PaginatedList<ClientBillableItemDto> InvoicedItems);
