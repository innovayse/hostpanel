using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Billing.Commands.RefundInvoicePayment;

/// <summary>
/// Refunds all or part of what a paid invoice took and writes the client ledger row with it. A
/// Gateway refund goes back through whatever took the money: the hosted-gateway plugin of the
/// invoice's session, or the card gateway against the original card payment's reference.
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
/// Once the claim is saved, the gateway calls and the recording run on
/// <see cref="CancellationToken.None"/>: a caller that disconnects must not abandon a refund whose
/// money has already moved. A recording save that fails (another writer touched the invoice, a
/// transient database error) is retried on a freshly loaded invoice, so a refund the gateway issued
/// is never left off the ledger.
/// </para>
/// <para>
/// Every card refund part carries an idempotency key built from the invoice, the charge and how much
/// of that charge the ledger already shows refunded. When a refund dies between the gateway and the
/// ledger, its claim stays until <see cref="Invoice.RefundClaimLease"/> runs out; the next refund
/// then reads the same ledger, plans the same parts under the same keys, and the gateway replays the
/// refunds it already issued instead of returning the money twice. A retry asking for a different
/// amount reuses a key with other parameters, which the gateway refuses rather than refunds.
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

    /// <summary>The card gateway that refunds a payment taken by card.</summary>
    private readonly ICardPaymentGateway _cardGateway;

    /// <summary>Puts a CreditBalance refund on the client's balance in the same save as the ledger row.</summary>
    private readonly IClientCredit _clientCredit;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Structured logger; records a gateway call that threw instead of answering.</summary>
    private readonly ILogger<RefundInvoicePaymentHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="pluginResolver">Resolves the hosted-gateway plugin for the invoice's gateway.</param>
    /// <param name="cardGateway">The card gateway that refunds a payment taken by card.</param>
    /// <param name="clientCredit">Puts a CreditBalance refund on the client's balance in the same save as the ledger row.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    /// <param name="logger">Structured logger.</param>
    public RefundInvoicePaymentHandler(
        IHostpanelDbContext context,
        IPaymentPluginResolver pluginResolver,
        ICardPaymentGateway cardGateway,
        IClientCredit clientCredit,
        IClock clock,
        ILogger<RefundInvoicePaymentHandler> logger)
    {
        _context = context;
        _pluginResolver = pluginResolver;
        _cardGateway = cardGateway;
        _clientCredit = clientCredit;
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
    /// or more than the card charges or the hosted gateway's payments still hold; <c>CLIENT_NOT_FOUND</c>
    /// when a CreditBalance refund has no client to credit; <c>INVOICE_NOT_GATEWAY_PAID</c> when a
    /// Gateway refund is asked for money no gateway took; <c>PAYMENT_METHOD_UNAVAILABLE</c> when the
    /// card gateway that took it is no longer configured; <c>CARD_REFUND_REFUSED</c> when the card
    /// gateway refuses or fails a refund (the parts it already returned are recorded);
    /// <c>GATEWAY_REFUND_REFUSED</c> when the hosted gateway fails it; or
    /// <c>PAYMENT_PLUGIN_NOT_CONFIGURED</c> when the session's plugin is gone. A refund that lost
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

        var toCreditBalance = cmd.RefundType == RefundType.CreditBalance;
        var refunds = new List<RefundOutcome>();
        var gatewayOutcome = Result.Success();
        switch (cmd.RefundType)
        {
            case RefundType.Manual:
                // RefundInvoicePaymentValidator requires a non-empty Gateway and RefundTransactionId
                // whenever RefundType is Manual, so neither is null on this branch.
                refunds.Add(new RefundOutcome(cmd.Gateway!, cmd.RefundTransactionId!, cmd.Amount));
                break;

            case RefundType.CreditBalance:
                refunds.Add(new RefundOutcome(
                    PaymentMethodNames.CreditBalance, $"credit-refund-{invoice.Id}-{now.Ticks}", cmd.Amount));
                break;

            default:
                gatewayOutcome = await RefundAtGatewayAsync(invoice, cmd.Amount, now, refunds, ct);
                if (refunds.Count > 0)
                {
                    await RecordGatewayRefundsAsync(invoice, refunds, now, cmd.Notes);
                    return gatewayOutcome;
                }

                break;
        }

        // A gateway that returned nothing leaves nothing to record; a claim it cleanly refused was
        // released and is saved here, so the next refund need not wait for the lease.
        if (refunds.Count == 0)
        {
            await _context.SaveChangesAsync(ct);
            return gatewayOutcome;
        }

        // A Manual or CreditBalance refund moves no gateway money, so it is recorded on the caller's token.
        foreach (var refund in refunds)
        {
            var ledgerRow = invoice.AddRefund(
                now, refund.Gateway, refund.Reference, refund.Amount, 0m, cmd.Notes, addedToCredit: toCreditBalance);
            _context.Transactions.Add(ledgerRow);
        }

        if (toCreditBalance)
        {
            var credited = await _clientCredit.AddAsync(new ClientCreditChange(invoice.ClientId, cmd.Amount), ct);
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

        // Otherwise the money came by card only when the ledger holds card payments for this invoice;
        // each payment's reference is a charge the card gateway refunds, at most for what it took.
        var cardRows = await _context.Transactions
            .AsNoTracking()
            .Where(t => t.InvoiceId == invoice.Id && t.PaymentMethod == PaymentMethodNames.Card)
            .ToListAsync(ct);
        var charges = cardRows
            .Where(t => t.AmountIn > 0)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id)
            .ToList();
        if (charges.Count == 0)
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_NOT_GATEWAY_PAID);
        }

        if (!_cardGateway.IsConfigured)
        {
            return Result.FailureCode(HostpanelErrorCodes.PAYMENT_METHOD_UNAVAILABLE);
        }

        var plan = PlanCardRefunds(invoice.Id, charges, cardRows.Sum(t => t.AmountOut), amount, invoice.Currency);
        if (plan.Sum(p => p.Amount) < amount)
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_REFUND_EXCEEDS_PAID);
        }

        await ClaimAsync(invoice, now, ct);
        foreach (var instruction in plan)
        {
            CardRefundResult refund;
            try
            {
                // Not the caller's token: a disconnect must not abandon a refund already under way.
                refund = await _cardGateway.RefundAsync(instruction, CancellationToken.None);
            }
            catch (Exception ex)
            {
                // The outcome of this charge is unknown (a timeout included), so the claim stays
                // until its lease runs out; the retry after it replays this part by its idempotency
                // key. The remaining charges are not asked, and the parts already returned are
                // still recorded by the caller.
                _logger.LogError(
                    ex, "Card refund of invoice {InvoiceId} threw for charge {ChargeId}; {Recorded} part(s) already returned are recorded.",
                    invoice.Id, instruction.TransactionId, refunded.Count);
                return Result.FailureCode(HostpanelErrorCodes.CARD_REFUND_REFUSED);
            }

            if (!refund.Success)
            {
                // A clean refusal: this charge returned nothing, so the claim is given back.
                invoice.ReleaseRefundClaim(now);
                return Result.FailureCode(HostpanelErrorCodes.CARD_REFUND_REFUSED);
            }

            refunded.Add(new RefundOutcome(PaymentMethodNames.Card, refund.RefundId!, instruction.Amount));
        }

        return Result.Success();
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

        await ClaimAsync(invoice, now, ct);
        string reference;
        try
        {
            // Not the caller's token: a disconnect must not abandon a refund already under way.
            reference = await plugin.RefundAsync(gatewayOrderId, amountMinor, CancellationToken.None);
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
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes once the claim is saved.</returns>
    private async Task ClaimAsync(Invoice invoice, DateTimeOffset now, CancellationToken ct)
    {
        invoice.BeginRefund(now);
        await _context.SaveChangesAsync(ct);
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
                        now, refund.Gateway, refund.Reference, refund.Amount, 0m, notes, addedToCredit: false));
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

    /// <summary>
    /// Spreads a card refund over the invoice's card charges, newest first, never asking a charge for
    /// more than it still holds. Each part is keyed by the invoice, the charge and what the ledger
    /// already shows refunded from that charge, so replanning the same refund yields the same keys.
    /// </summary>
    /// <param name="invoiceId">The invoice refunded.</param>
    /// <param name="chargesNewestFirst">The invoice's card charges, newest first.</param>
    /// <param name="alreadyRefunded">What earlier card refunds of this invoice returned.</param>
    /// <param name="amount">The amount to refund now.</param>
    /// <param name="currency">The invoice currency.</param>
    /// <returns>
    /// One refund instruction per charge that takes part; their sum falls short of
    /// <paramref name="amount"/> when the charges cannot cover it.
    /// </returns>
    private static List<CardRefundInstruction> PlanCardRefunds(
        int invoiceId,
        IReadOnlyList<Transaction> chargesNewestFirst, decimal alreadyRefunded, decimal amount, string currency)
    {
        var plan = new List<CardRefundInstruction>();
        var remaining = amount;
        foreach (var charge in chargesNewestFirst)
        {
            // Earlier refunds were spread newest first as well, so they used up the newest charges first.
            var used = Math.Min(charge.AmountIn, alreadyRefunded);
            alreadyRefunded -= used;
            var part = Math.Min(charge.AmountIn - used, remaining);
            if (part > 0)
            {
                var usedMinor = CurrencyCode.ToMinorUnits(used, currency);
                plan.Add(new CardRefundInstruction(
                    charge.TransactionId, part, currency, $"refund-{invoiceId}-{charge.TransactionId}-{usedMinor}"));
                remaining -= part;
            }
        }

        return plan;
    }
}
