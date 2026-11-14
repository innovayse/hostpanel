using System.Linq.Expressions;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Currencies.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListClientBillableItems;

/// <summary>
/// Returns uninvoiced items (all) and invoiced items (paginated) for a client, both narrowed by the search text and the
/// type filter in SQL.
/// </summary>
public sealed class ListClientBillableItemsHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    public ListClientBillableItemsHandler(IHostpanelDbContext context)
    {
        _context = context;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="qry">The client, the search and type filters and the invoiced list's paging window.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with uninvoiced items, their totals per currency, and paginated invoiced items. Never refuses.
    /// </returns>
    public async Task<Result<ClientBillableItemsResultDto>> HandleAsync(
        ListClientBillableItemsQuery qry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(qry);

        // One narrowing for both lists, so the search and the type can never leave one of them out.
        var clientItems = _context.BillableItems
            .AsNoTracking()
            .Where(x => x.ClientId == qry.ClientId)
            .Matching(qry);

        var uninvoicedDtos = await clientItems
            .Where(x => !x.IsInvoiced)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Select(MapProjection())
            .ToListAsync(ct);
        // Amounts in different currencies are never added together: one total per currency.
        var uninvoicedTotal = uninvoicedDtos
            .GroupBy(x => x.Currency)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new CurrencyAmountDto(g.Sum(x => x.Amount), g.Key))
            .ToList();

        // The id ends the order so a page never repeats or skips a row between requests.
        var invoicedQuery = clientItems
            .Where(x => x.IsInvoiced)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id);
        var totalCount = await invoicedQuery.CountAsync(ct);
        var invoicedDtos = await invoicedQuery
            .Paginate(qry.Page, qry.PageSize)
            .Select(MapProjection())
            .ToListAsync(ct);

        var pagedInvoiced = new PaginatedList<ClientBillableItemDto>(
            invoicedDtos, totalCount, qry.Page, qry.PageSize);

        return Result<ClientBillableItemsResultDto>.Success(
            new ClientBillableItemsResultDto(uninvoicedDtos, uninvoicedTotal, pagedInvoiced));
    }

    /// <summary>The EF projection from a domain billable item to its client-scoped DTO.</summary>
    /// <returns>The projection expression, reused for both the uninvoiced and invoiced pages.</returns>
    private static Expression<Func<BillableItem, ClientBillableItemDto>> MapProjection() =>
        item => new ClientBillableItemDto(
            item.Id, item.ClientId, item.Description, item.Amount, item.Currency, item.Type,
            item.RecurringPeriod, item.IsInvoiced, item.InvoiceId, item.NextDueDate, item.CreatedAt);
}
