using System.Globalization;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Reports.Common;
using Innovayse.Hostpanel.Application.Reports.Common.Enums;
using Innovayse.Hostpanel.Application.Reports.Extensions;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Reports.Queries.ClientStatement;

/// <summary>Handles <see cref="ClientStatementQuery"/>.</summary>
/// <remarks>
/// <para>
/// The statement is the client's whole account with us, the credit balance included: a positive
/// balance is what the client owes, a negative one what we hold for them. Its lines are:
/// </para>
/// <list type="bullet">
/// <item>
/// A debit per invoice that asks for money (Unpaid, Overdue, Paid, PartiallyRefunded or Refunded; a draft, a
/// cancelled invoice, one in collections or one waiting for its gateway is not a charge). The debit
/// is the invoice's full amount, <see cref="Invoice.Total"/> plus the <see cref="Invoice.Credit"/>
/// applied to it, less what was refunded on it.
/// </item>
/// <item>
/// Every ledger transaction: money in is a credit, money out a debit. The one exception is a
/// refund kept on the credit balance (payment method <see cref="PaymentMethodNames.CreditBalance"/>):
/// no money left, the refund already lowered the invoice's debit and the amount now sits in the
/// balance, so a debit for it would count it twice.
/// </item>
/// <item>
/// A credit for each invoice that arrived already paid (imported history: settled with no payment
/// recorded here and a total above zero), dated when it was paid, so that history nets to zero
/// instead of reading as a debt.
/// </item>
/// </list>
/// <para>
/// Applying credit to an invoice writes no ledger row: it moves the amount from the balance to
/// <see cref="Invoice.Credit"/> and lowers <see cref="Invoice.Total"/>. The money that funded the
/// balance is already on the statement (the deposit's ledger row, or the refund that lowered an
/// earlier invoice), so the invoice is charged in full rather than net of that credit; charging
/// only <see cref="Invoice.Total"/> would leave the funding credit standing with nothing against it.
/// An invoice marked paid out of the balance (<see cref="Invoice.MarkPaid"/> once credit covered it)
/// has a zero total and records no payment, so it gets no history credit either.
/// </para>
/// </remarks>
public sealed class ClientStatementHandler
{
    /// <summary>Invoice statuses that put a charge on the statement.</summary>
    private static readonly InvoiceStatus[] ChargedStatuses =
        [InvoiceStatus.Unpaid, InvoiceStatus.Overdue, InvoiceStatus.Paid, InvoiceStatus.PartiallyRefunded, InvoiceStatus.Refunded];

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The source of the current moment.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clock">The source of the current moment.</param>
    public ClientStatementHandler(IHostpanelDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The client and the days to statement.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The statement with its running balance, or <c>CLIENT_NOT_FOUND</c> when the id names no
    /// client.
    /// </returns>
    public async Task<Result<ClientStatementDto>> HandleAsync(ClientStatementQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var client = await _context.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == query.ClientId, ct);
        if (client is null)
        {
            return Result<ClientStatementDto>.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND);
        }

        var from = query.From?.StartOfDayUtc();
        var until = query.To?.EndOfDayExclusiveUtc() ?? _clock.UtcNow;

        var invoices = await _context.Invoices
            .AsNoTracking()
            .Where(i => i.ClientId == query.ClientId && ChargedStatuses.Contains(i.Status) && i.CreatedAt < until)
            .Select(i => new
            {
                i.Id,
                i.CreatedAt,
                i.PaidAt,
                i.Currency,
                i.Total,
                Charge = i.Total + i.Credit - i.Transactions
                    .Where(t => t.Type == InvoiceTransactionType.Refund)
                    .Sum(t => t.Amount),
                PaidElsewhere = (Invoice.PaidStatuses.Contains(i.Status) || i.Status == InvoiceStatus.Refunded)
                    && i.Total > 0
                    && !i.Transactions.Any(t => t.Type == InvoiceTransactionType.Payment),
            })
            .ToListAsync(ct);

        var transactions = await _context.Transactions
            .AsNoTracking()
            .Where(t => t.ClientId == query.ClientId
                && t.Date < until
                && t.PaymentMethod != PaymentMethodNames.CreditBalance)
            .Select(t => new { t.Date, t.Description, t.Currency, t.AmountIn, t.AmountOut })
            .ToListAsync(ct);

        // Merged and sorted here: the sources have unrelated shapes and the running balance below
        // needs one interleaved timeline, which SQL cannot hand back directly.
        var lines = new List<ClientStatementSource>();
        foreach (var inv in invoices)
        {
            lines.Add(new ClientStatementSource(inv.CreatedAt, StatementLineType.Invoice, inv.Id, null, inv.Currency, inv.Charge, 0m));
            if (inv.PaidElsewhere)
            {
                lines.Add(new ClientStatementSource(
                    inv.PaidAt ?? inv.CreatedAt, StatementLineType.PaidHistory, inv.Id, null, inv.Currency, 0m, inv.Total));
            }
        }

        foreach (var tx in transactions)
        {
            lines.Add(new ClientStatementSource(tx.Date, StatementLineType.Transaction, null, tx.Description, tx.Currency, tx.AmountOut, tx.AmountIn));
        }

        // A history payment can carry the moment it was imported, after the period ends.
        lines.RemoveAll(l => l.Date >= until);
        lines.Sort((a, b) => a.Date.CompareTo(b.Date));

        // Amounts of different currencies are never added: the balance runs separately per currency.
        var previousBalances = lines
            .Where(l => l.Date < from)
            .GroupBy(l => l.Currency)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Debit - l.Credit));

        var running = new Dictionary<string, decimal>(previousBalances);
        var result = new List<ClientStatementLineDto>();
        foreach (var line in lines.Where(l => from is null || l.Date >= from))
        {
            var balance = running.GetValueOrDefault(line.Currency) + line.Debit - line.Credit;
            running[line.Currency] = balance;
            result.Add(new ClientStatementLineDto(
                line.Type,
                line.Date.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                line.InvoiceId,
                line.Description,
                line.Currency,
                line.Debit,
                line.Credit,
                balance));
        }

        var balances = lines
            .Select(l => l.Currency)
            .Distinct()
            .Order(StringComparer.Ordinal)
            .Select(currency => new ClientStatementBalanceDto(
                currency,
                previousBalances.GetValueOrDefault(currency),
                running.GetValueOrDefault(currency)))
            .ToList();

        return Result<ClientStatementDto>.Success(
            new ClientStatementDto(query.ClientId, $"{client.FirstName} {client.LastName}", balances, result));
    }
}
