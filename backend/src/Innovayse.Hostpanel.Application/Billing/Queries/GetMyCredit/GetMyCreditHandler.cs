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

namespace Innovayse.Hostpanel.Application.Billing.Queries.GetMyCredit;

/// <summary>Returns the credit of the account the caller acts for: balance, top-up limits and a page of the history, newest first.</summary>
public sealed class GetMyCreditHandler
{
    /// <summary>The module's data context, for the credit history.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the account the caller acts for, owner or linked user; the query does not say, and must not.</summary>
    private readonly IClientAccess _access;

    /// <summary>Wolverine bus, for the client's balance and billing currency.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Reads the top-up limits the admin set.</summary>
    private readonly CreditTopUpSettingsReader _settings;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context, for the credit history.</param>
    /// <param name="access">Resolves the account the caller acts for.</param>
    /// <param name="bus">Wolverine bus, for the client's balance and billing currency.</param>
    /// <param name="settings">Reads the top-up limits the admin set.</param>
    public GetMyCreditHandler(
        IHostpanelDbContext context, IClientAccess access, IMessageBus bus, CreditTopUpSettingsReader settings)
    {
        _context = context;
        _access = access;
        _bus = bus;
        _settings = settings;
    }

    /// <summary>Handles <see cref="GetMyCreditQuery"/>.</summary>
    /// <param name="query">The query. It names no account: this reads the caller's own.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with the credit, or <c>CLIENT_PROFILE_NOT_FOUND</c> when the caller acts for no account.
    /// </returns>
    public async Task<Result<MyCreditDto>> HandleAsync(GetMyCreditQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var clientId = await _access.RequireClientIdAsync(ct);
        if (clientId.IsFailure)
        {
            return Result<MyCreditDto>.FailureCode(clientId.ErrorCode!);
        }

        var client = await _bus.FindClientBriefAsync(clientId.Value, withEmail: false, ct);
        if (client is null)
        {
            return Result<MyCreditDto>.FailureCode(HostpanelErrorCodes.CLIENT_PROFILE_NOT_FOUND);
        }

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, PagingLimits.MinPageSize, PagingLimits.MaxPageSize);

        // Only the entries that moved the balance are history; the cash ledger beside them is not shown here.
        var history = _context.Transactions
            .AsNoTracking()
            .Where(t => t.ClientId == clientId.Value && t.CreditMovement != null)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id);
        var total = await history.CountAsync(ct);
        var entries = await history
            .Paginate(page, pageSize)
            .Select(t => new MyCreditEntryDto(
                t.Id, t.Date, t.CreditMovement!.Value, t.CreditChange!.Value, t.BalanceAfter!.Value, t.InvoiceId))
            .ToListAsync(ct);

        var currency = await _context.GetCreditCurrencyAsync(client.Id, client.Currency, client.CreditBalance, ct);
        var limits = await _settings.ReadAsync(ct);

        return Result.Success(new MyCreditDto(
            client.CreditBalance,
            currency,
            limits.Minimum,
            limits.Maximum,
            new PaginatedList<MyCreditEntryDto>(entries, total, page, pageSize)));
    }
}
