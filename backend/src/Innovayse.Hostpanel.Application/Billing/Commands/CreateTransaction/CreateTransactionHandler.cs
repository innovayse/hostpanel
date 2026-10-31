using Innovayse.Hostpanel.Application.Audit.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.CreateTransaction;

/// <summary>
/// Creates a <see cref="Transaction"/> and optionally adjusts the client's credit balance.
/// </summary>
public sealed class CreateTransactionHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Records the activity log line for this action, saved with the change it describes.</summary>
    private readonly IActivityLogger _activity;

    /// <summary>Resolves the currency of a transaction that settles no invoice: the client's.</summary>
    private readonly IPayerCurrencyResolver _payerCurrency;

    /// <summary>Adjusts the client's credit balance in the same save as the ledger row.</summary>
    private readonly IClientCredit _clientCredit;

    /// <summary>Wolverine bus the owning modules' existence queries are dispatched through.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="activity">Records the activity log line for this action, saved with the change it describes.</param>
    /// <param name="payerCurrency">Resolves the currency of a transaction that settles no invoice.</param>
    /// <param name="clientCredit">Adjusts the client's credit balance in the same save as the ledger row.</param>
    /// <param name="bus">Wolverine bus the owning modules' existence queries are dispatched through.</param>
    public CreateTransactionHandler(
        IHostpanelDbContext context,
        IActivityLogger activity,
        IPayerCurrencyResolver payerCurrency,
        IClientCredit clientCredit,
        IMessageBus bus)
    {
        _context = context;
        _activity = activity;
        _payerCurrency = payerCurrency;
        _clientCredit = clientCredit;
        _bus = bus;
    }

    /// <summary>Handles <see cref="CreateTransactionCommand"/>.</summary>
    /// <param name="cmd">The create command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success with the newly created transaction's ID, <c>CLIENT_NOT_FOUND</c> when
    /// <paramref name="cmd"/> names a client that does not exist, <c>INVOICE_NOT_FOUND</c> when it
    /// names an invoice that does not exist, or <c>CLIENT_CREDIT_INSUFFICIENT</c> when the amount
    /// out is more than the balance holds.
    /// </returns>
    public async Task<Result<int>> HandleAsync(CreateTransactionCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        if (!await _bus.ClientExistsAsync(cmd.ClientId, ct))
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.CLIENT_NOT_FOUND);
        }

        if (cmd.InvoiceId is { } linkedInvoiceId && !await _context.InvoiceExistsAsync(linkedInvoiceId, ct))
        {
            return Result<int>.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        // The amounts are in the currency of the invoice they settle; with no invoice, the client's.
        var invoiceCurrency = cmd.InvoiceId is { } invoiceId
            ? await _context.Invoices.AsNoTracking()
                .Where(i => i.Id == invoiceId)
                .Select(i => i.Currency)
                .FirstOrDefaultAsync(ct)
            : null;
        var currency = invoiceCurrency ?? (await _payerCurrency.ForClientAsync(cmd.ClientId, ct)).Code;

        var transaction = Transaction.Create(
            cmd.ClientId,
            cmd.Date,
            cmd.Description,
            cmd.TransactionId,
            cmd.InvoiceId,
            cmd.PaymentMethod,
            cmd.AmountIn,
            cmd.AmountOut,
            cmd.Fees,
            cmd.AddToCredit,
            currency,
            TransactionSource.Manual);

        _context.Transactions.Add(transaction);

        if (cmd.AddToCredit && cmd.AmountIn > 0)
        {
            var added = await _clientCredit.AddAsync(new ClientCreditChange(cmd.ClientId, cmd.AmountIn), ct);
            if (added.IsFailure)
            {
                return Result<int>.FailureCode(added.ErrorCode!);
            }
        }

        if (cmd.AddToCredit && cmd.AmountOut > 0)
        {
            var taken = await _clientCredit.DeductAsync(new ClientCreditChange(cmd.ClientId, cmd.AmountOut), ct);
            if (taken.IsFailure)
            {
                return Result<int>.FailureCode(taken.ErrorCode!);
            }
        }

        await _context.SaveChangesAsync(ct);

        await _activity.RecordAsync(
            new ActivityLogEntry(cmd.ClientId, $"Added Transaction - Transaction ID: {transaction.Id}"), ct);
        await _context.SaveChangesAsync(ct);

        return Result<int>.Success(transaction.Id);
    }
}
