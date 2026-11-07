using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Extensions;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Queries.GetClientCredit;

/// <summary>Returns one client's credit for staff: the balance and a page of the history, newest first.</summary>
public sealed class GetClientCreditHandler
{
    /// <summary>The module's data context, for the credit history.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine bus, for the client's balance and billing currency.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context, for the credit history.</param>
    /// <param name="bus">Wolverine bus, for the client's balance and billing currency.</param>
    public GetClientCreditHandler(IHostpanelDbContext context, IMessageBus bus)
    {
        _context = context;
        _bus = bus;
    }

    /// <summary>Handles <see cref="GetClientCreditQuery"/>.</summary>
    /// <param name="query">The query naming the client and the page.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success with the client's credit, or <c>CLIENT_NOT_FOUND</c> when the id names no client.</returns>
    public async Task<Result<ClientCreditDto>> HandleAsync(GetClientCreditQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var client = await _bus.FindClientBriefAsync(query.ClientId, withEmail: false, ct);
        if (client is null)
        {
            return Result<ClientCreditDto>.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND);
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, PagingLimits.MinPageSize, PagingLimits.MaxPageSize);

        // Only the entries that moved the balance are history; the cash ledger beside them has its own list.
        var history = _context.Transactions
            .AsNoTracking()
            .Where(t => t.ClientId == query.ClientId && t.CreditMovement != null)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id);
        var total = await history.CountAsync(ct);
        var entries = await history
            .Paginate(page, pageSize)
            .Select(t => new CreditEntryDto(
                t.Id, t.Date, t.CreditMovement!.Value, t.CreditChange!.Value, t.BalanceAfter!.Value, t.InvoiceId, t.Description))
            .ToListAsync(ct);

        var currency = await _context.GetCreditCurrencyAsync(client.Id, client.Currency, client.CreditBalance, ct);
        return Result.Success(new ClientCreditDto(
            client.Id, client.CreditBalance, currency, new PaginatedList<CreditEntryDto>(entries, total, page, pageSize)));
    }
}
