using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Hostpanel.Sdk.Plugins.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Billing.Commands.RefundInvoicePayment;

/// <summary>
/// Refunds all or part of what a paid invoice took and writes the client ledger row with it. A
/// Gateway refund goes back through the hosted-gateway plugin of the invoice's session.
/// </summary>
/// <remarks>
/// Every refund changes the invoice row (<see cref="Invoice.RefundStartedAt"/>), so two refunds
/// racing on one invoice cannot both pass <see cref="Invoice.RefundableAmount"/>: the later save
/// fails the row's concurrency check and answers 409. A Gateway refund saves that claim
/// (<see cref="Invoice.BeginRefund"/>) before it calls the gateway, so the race is lost before any
/// money moves: the loser either fails the claim save or finds the claim held and answers
/// <c>INVOICE_REFUND_CONFLICT</c>. Whatever the gateway did return is recorded afterwards, even when
/// a later part was refused or threw.
/// <para>
/// Before the claim, every enabled fulfilment plugin is asked how much may be refunded
/// (<see cref="RefundAdviser"/>): the refund is reduced to the smallest answer with the plugin's reason
/// added to the notes, refused when an answer is zero, and refused when a plugin cannot answer.
/// </para>
/// <para>
/// Once the claim is saved, the gateway calls and the recording run on
/// <see cref="CancellationToken.None"/>: a caller that disconnects must not abandon a refund whose
/// money has already moved (the request token is used only up to and including the claim save, and for Manual and
/// CreditBalance refunds, which move no gateway money). A recording save that fails (another writer touched the invoice, a
/// transient database error) is retried on a freshly loaded invoice, so a refund the gateway issued
/// is never left off the ledger.
/// </para>
/// <para>
/// When a refund dies between the gateway and the ledger, its claim stays until
/// <see cref="Invoice.RefundClaimLease"/> runs out, so no blind retry returns the money twice. The claim also holds the
/// refund's id (<see cref="Invoice.PendingRefundId"/>), saved before the gateway is called and sent to the plugin in a
/// <see cref="RefundInstruction"/>; a retry of the same amount after the lease reuses it, so a plugin that passes it on as
/// the gateway's idempotency key cannot refund twice. The instruction also carries what the ledger already holds as
/// refunded through the gateway order (<see cref="RefundInstruction.AlreadyRefundedMinor"/>), so a gateway without an
/// idempotency key can tell a retry from a new partial refund.
/// </para>
/// </remarks>
public sealed class RefundInvoicePaymentHandler
{
    /// <summary>How many times the save that records gateway refunds is tried before it gives up.</summary>
    private const int MaxRecordAttempts = 3;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Resolves the hosted-gateway payment plugin that processed the original payment.</summary>
    private readonly IPaymentPluginResolver _pluginResolver;

    /// <summary>Puts a CreditBalance refund on the client's balance in the same save as the ledger row.</summary>
    private readonly IClientCredit _clientCredit;

    /// <summary>Asks the enabled fulfilment plugins to cap the refund before any money moves.</summary>
    private readonly RefundAdviser _adviser;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Structured logger; records a gateway call that threw instead of answering.</summary>
    private readonly ILogger<RefundInvoicePaymentHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="pluginResolver">Resolves the hosted-gateway plugin for the invoice's gateway.</param>
    /// <param name="clientCredit">Puts a CreditBalance refund on the client's balance in the same save as the ledger row.</param>
    /// <param name="adviser">Asks the enabled fulfilment plugins to cap the refund before any money moves.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    /// <param name="logger">Structured logger.</param>
    public RefundInvoicePaymentHandler(
        IHostpanelDbContext context,
        IPaymentPluginResolver pluginResolver,
        IClientCredit clientCredit,
        RefundAdviser adviser,
        IClock clock,
        ILogger<RefundInvoicePaymentHandler> logger)
    {
        _context = context;
        _pluginResolver = pluginResolver;
        _clientCredit = clientCredit;
        _adviser = adviser;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Handles <see cref="RefundInvoicePaymentCommand"/>.</summary>
    /// <param name="cmd">The refund command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Success, or <c>INVOICE_NOT_FOUND</c>; <c>INVOICE_NOT_PAID</c> when the invoice is not Paid or
    /// PartiallyRefunded;
    /// <c>INVOICE_REFUND_CONFLICT</c> when another refund of the invoice is still waiting for its
    /// gateway; <c>INVOICE_REFUND_EXCEEDS_PAID</c> when the amount is more than is still refundable,
    /// or more than the hosted gateway's payments still hold; <c>CLIENT_NOT_FOUND</c>
    /// when a CreditBalance refund has no client to credit; <c>INVOICE_NOT_GATEWAY_PAID</c> when a
    /// Gateway refund is asked for money no gateway took; <c>GATEWAY_REFUND_REFUSED</c> when the hosted gateway fails it; or
    /// <c>PAYMENT_PLUGIN_NOT_CONFIGURED</c> when the session's plugin is gone; <c>REFUND_ADVICE_UNAVAILABLE</c> when an
    /// enabled fulfilment plugin could not say how much may be refunded, or <c>REFUND_BLOCKED_BY_PLUGIN</c> when one allows
    /// none. A refund that lost
    /// the race for the invoice row surfaces as the concurrency conflict (409).
    /// </returns>
    public async Task<Result> HandleAsync(RefundInvoicePaymentCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var invoice = await _context.Invoices
            .Include(i => i.Items)
            .Include(i => i.Transactions)
            .FirstOrDefaultAsync(i => i.Id == cmd.InvoiceId, ct);
        if (invoice is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        // Checked before any money moves; the domain repeats them when the refund is recorded.
        if (!invoice.IsPaid)
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_NOT_PAID);
        }

        var now = _clock.UtcNow;
        if (invoice.IsRefundInFlight(now))
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_REFUND_CONFLICT);
        }

        if (cmd.Amount > invoice.RefundableAmount)
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_REFUND_EXCEEDS_PAID);
        }

        // Asked before any money moves or the invoice is claimed: a plugin may cap the amount, and one that
        // cannot answer refuses the refund. The automatic refund of a failed order arrives here as well.
        var advised = await _adviser.AdviseAsync(invoice, cmd.Amount, cmd.Notes, ct);
        if (advised.IsFailure)
        {
            return Result.FailureCode(advised.ErrorCode!);
        }

        var amount = advised.Value!.Amount;
        var notes = advised.Value.Notes;

        var toCreditBalance = cmd.RefundType == RefundType.CreditBalance;
        var refunds = new List<RefundOutcome>();
        var gatewayOutcome = Result.Success();
        switch (cmd.RefundType)
        {
            case RefundType.Manual:
                // RefundInvoicePaymentValidator requires a non-empty Gateway and RefundTransactionId
                // whenever RefundType is Manual, so neither is null on this branch.
                refunds.Add(new RefundOutcome(cmd.Gateway!, cmd.RefundTransactionId!, amount));
                break;

            case RefundType.CreditBalance:
                refunds.Add(new RefundOutcome(
                    PaymentMethodNames.CreditBalance, $"credit-refund-{invoice.Id}-{now.Ticks}", amount));
                break;

            default:
                gatewayOutcome = await RefundAtGatewayAsync(invoice, amount, now, refunds, ct);
                if (refunds.Count > 0)
                {
                    await RecordGatewayRefundsAsync(invoice, refunds, now, notes);
                    return gatewayOutcome;
                }

                break;
        }

        // A gateway that returned nothing leaves nothing to record. Its claim is kept on purpose: the plugin reports a refusal
        // and a fault alike by throwing, so the outcome is unknown and the claim (with its refund id) waits out its lease.
        if (refunds.Count == 0)
        {
            return gatewayOutcome;
        }

        // A Manual or CreditBalance refund moves no gateway money, so it is recorded on the caller's token.
        foreach (var refund in refunds)
        {
            var ledgerRow = invoice.AddRefund(
                now, refund.Gateway, refund.Reference, refund.Amount, 0m, notes, addedToCredit: toCreditBalance,
                source: TransactionSource.Manual);
            _context.Transactions.Add(ledgerRow);
        }

        if (toCreditBalance)
        {
            var credited = await _clientCredit.AddAsync(new ClientCreditChange(invoice.ClientId, amount), ct);
            if (credited.IsFailure)
            {
                return Result.FailureCode(credited.ErrorCode!);
            }
        }

        await _context.SaveChangesAsync(ct);
        return gatewayOutcome;
    }

    /// <summary>
    /// Sends the refund back through whatever took the money, once every check that needs no
    /// gateway has passed and the invoice has been claimed for it.
    /// </summary>
    /// <param name="invoice">The paid invoice.</param>
    /// <param name="amount">The amount to refund, in the invoice currency.</param>
    /// <param name="now">The moment the refund begins; its claim and its parts carry it.</param>
    /// <param name="refunded">Receives every refund the gateway issued, to be recorded.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success once the whole amount is refunded, or the refusal code.</returns>
    private async Task<Result> RefundAtGatewayAsync(
        Invoice invoice, decimal amount, DateTimeOffset now, List<RefundOutcome> refunded, CancellationToken ct)
    {
        // The stored hosted-gateway session took the money only when a payment was recorded through
        // it; a session kept after another path settled the invoice took none of it.
        if (invoice is { GatewayModule: { } module, GatewayOrderId: { } orderId } && invoice.WasPaidThrough(module))
        {
            return await RefundAtHostedGatewayAsync(invoice, module, orderId, amount, now, refunded, ct);
        }

        // No other path takes gateway money: a payment that no hosted-gateway session recorded is
        // not refundable through a gateway (a manual refund records it instead).
        return Result.FailureCode(HostpanelErrorCodes.INVOICE_NOT_GATEWAY_PAID);
    }

    /// <summary>
    /// Refunds through the hosted-gateway plugin that took the money, never more than the payments
    /// recorded through that gateway still hold.
    /// </summary>
    /// <param name="invoice">The paid invoice.</param>
    /// <param name="module">The plugin id of the invoice's kept gateway session.</param>
    /// <param name="gatewayOrderId">The gateway-side order the money was paid on.</param>
    /// <param name="amount">The amount to refund, in the invoice currency.</param>
    /// <param name="now">The moment the refund begins; its claim and its part carry it.</param>
    /// <param name="refunded">Receives the refund the gateway issued, to be recorded.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success once refunded, or the refusal code.</returns>
    private async Task<Result> RefundAtHostedGatewayAsync(
        Invoice invoice,
        string module,
        string gatewayOrderId,
        decimal amount,
        DateTimeOffset now,
        List<RefundOutcome> refunded,
        CancellationToken ct)
    {
        var plugin = await _pluginResolver.ResolveAsync(module, ct);
        if (plugin is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.PAYMENT_PLUGIN_NOT_CONFIGURED);
        }

        // Money paid another way (a manual part payment, credit) cannot go back through this gateway.
        if (amount > invoice.RefundableThrough(module))
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_REFUND_EXCEEDS_PAID);
        }

        // Rounded to minor units once: the gateway is sent and the ledger records the same amount.
        var amountMinor = CurrencyCode.ToMinorUnits(amount, invoice.Currency);
        var refundedAmount = CurrencyCode.FromMinorUnits(amountMinor, invoice.Currency);

        // The id is saved with the claim, before the gateway is called, and is the gateway's idempotency key: a retry of
        // this refund after a timeout or a crash sends the same id and cannot refund twice.
        var refundId = await ClaimAsync(invoice, now, refundedAmount, ct);

        // What the ledger already holds as refunded at this gateway, so a gateway with no idempotency key can tell a retry
        // of this refund from a new partial one. The pending refund is not on the ledger until it is recorded, so it is
        // never counted; a manual or credit refund moved no gateway money and is left out. The source is a property of the
        // ledger row (Transaction), not of the invoice's own payment line, so the ledger is what is read.
        var gatewayRefunds = await _context.Transactions
            .AsNoTracking()
            .Where(t => t.InvoiceId == invoice.Id && t.Source == TransactionSource.GatewayRefund && t.PaymentMethod == module)
            .Select(t => t.AmountOut)
            .ToListAsync(ct);
        var alreadyRefundedMinor = gatewayRefunds.Sum(amountOut => CurrencyCode.ToMinorUnits(amountOut, invoice.Currency));
        string reference;
        try
        {
            // CancellationToken.None on purpose: the claim is saved, so a caller that disconnects must not abandon a refund
            // whose money may already be moving; the claim and its refund id make a later retry safe.
            reference = await plugin.RefundAsync(
                new RefundInstruction(gatewayOrderId, amountMinor, refundId.ToString("N"), alreadyRefundedMinor), CancellationToken.None);
        }
        catch (Exception ex)
        {
            // A plugin answers a refusal by throwing, and a fault looks the same, so the outcome is
            // unknown: the claim stays until its lease runs out rather than inviting a blind retry.
            _logger.LogError(ex, "Hosted gateway refund of invoice {InvoiceId} through {Module} failed.", invoice.Id, module);
            return Result.FailureCode(HostpanelErrorCodes.GATEWAY_REFUND_REFUSED);
        }

        refunded.Add(new RefundOutcome(module, reference, refundedAmount));
        return Result.Success();
    }

    /// <summary>
    /// Claims the invoice for this refund and saves the claim before any gateway is called. Of two
    /// refunds racing on the invoice the later save fails the row's concurrency check (409), so only
    /// one of them ever moves money.
    /// </summary>
    /// <param name="invoice">The paid invoice.</param>
    /// <param name="now">The moment the refund begins.</param>
    /// <param name="amount">The amount being refunded, in the invoice currency.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stable refund id, saved with the claim.</returns>
    private async Task<Guid> ClaimAsync(Invoice invoice, DateTimeOffset now, decimal amount, CancellationToken ct)
    {
        // The request token is right here: nothing has moved yet, so a caller that gives up simply leaves no claim.
        var refundId = invoice.BeginGatewayRefund(now, amount);
        await _context.SaveChangesAsync(ct);
        return refundId;
    }

    /// <summary>
    /// Records every refund the gateway issued on the invoice and the client ledger. A save that
    /// fails is retried on the invoice as the database now holds it, skipping a part already on it,
    /// because that money has left and the ledger must say so.
    /// </summary>
    /// <param name="invoice">The claimed invoice.</param>
    /// <param name="refunds">The refunds the gateway issued.</param>
    /// <param name="now">The moment the refund began; its parts are recorded under it.</param>
    /// <param name="notes">The operator's notes for the ledger rows.</param>
    /// <returns>A task that completes once every refund is saved.</returns>
    private async Task RecordGatewayRefundsAsync(
        Invoice invoice, IReadOnlyList<RefundOutcome> refunds, DateTimeOffset now, string? notes)
    {
        for (var attempt = 1; ; attempt++)
        {
            foreach (var refund in refunds)
            {
                if (!invoice.Transactions.Any(t => t.Type == InvoiceTransactionType.Refund && t.TransactionId == refund.Reference))
                {
                    _context.Transactions.Add(invoice.AddRefund(
                        now, refund.Gateway, refund.Reference, refund.Amount, 0m, notes, addedToCredit: false,
                        source: TransactionSource.GatewayRefund));
                }
            }

            try
            {
                // Not the caller's token: the money has moved, so the record is written regardless.
                await _context.SaveChangesAsync(CancellationToken.None);
                return;
            }
            catch (Exception ex) when (ex is ConcurrencyConflictException or DbUpdateException)
            {
                if (attempt == MaxRecordAttempts)
                {
                    _logger.LogCritical(
                        ex, "Gateway refunds {References} of invoice {InvoiceId} were issued but could not be recorded.",
                        string.Join(", ", refunds.Select(r => r.Reference)), invoice.Id);
                    throw;
                }

                _logger.LogWarning(
                    ex, "Recording the gateway refunds of invoice {InvoiceId} failed on attempt {Attempt}; retrying on a fresh read.",
                    invoice.Id, attempt);
                _context.DetachAll();
                invoice = await _context.Invoices
                    .Include(i => i.Items)
                    .Include(i => i.Transactions)
                    .FirstAsync(i => i.Id == invoice.Id, CancellationToken.None);

                // The parts are recorded under the claim's moment as the database stored it, which
                // can be coarser than the clock reading the claim was taken at.
                now = invoice.RefundStartedAt ?? now;
            }
        }
    }
}
