using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Queries.ListPendingBankTransfers;

/// <summary>
/// Lists the invoices whose client chose a manual bank transfer and waits for staff to confirm or reject it, oldest first so
/// the transfer that has waited longest is looked at first. The filter is <see cref="Invoice.BankTransferPendingFilter"/>,
/// the same one the confirm and reject actions guard with.
/// </summary>
public sealed class ListPendingBankTransfersHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The message bus, for the Clients module's client briefs; Billing never reads the client tables.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">The message bus, for the Clients module's client briefs.</param>
    public ListPendingBankTransfersHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The page to list.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with a page of waiting transfers. Never refuses.</returns>
    public async Task<Result<PaginatedList<PendingBankTransferDto>>> HandleAsync(
        ListPendingBankTransfersQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, PagingLimits.MinPageSize, PagingLimits.MaxPageSize);

        var waiting = _context.Invoices
            .AsNoTracking()
            .Where(Invoice.BankTransferPendingFilter)
            .OrderBy(i => i.BankTransferRequestedAt)
            .ThenBy(i => i.Id);

        var total = await waiting.CountAsync(ct);

        // Payments are loaded because what the transfer must be for is the total less what was already paid.
        var invoices = await waiting
            .Include(i => i.Transactions)
            .Paginate(page, pageSize)
            .ToListAsync(ct);

        var clients = await _bus.GetRequiredClientBriefsByIdAsync(
            invoices.Select(i => i.ClientId).Distinct().ToList(), ct);

        var rows = invoices
            .Select(i => new PendingBankTransferDto(
                i.Id,
                i.BankTransferReference,
                i.ClientId,
                clients[i.ClientId].FullName,
                i.Total,
                i.AmountDue,
                i.Currency,
                i.DueDate,
                i.BankTransferRequestedAt!.Value))
            .ToList();

        return Result<PaginatedList<PendingBankTransferDto>>.Success(
            new PaginatedList<PendingBankTransferDto>(rows, total, page, pageSize));
    }
}
