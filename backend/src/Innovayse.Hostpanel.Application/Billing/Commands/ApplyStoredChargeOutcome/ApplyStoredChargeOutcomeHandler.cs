using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Hostpanel.Sdk.Plugins.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ApplyStoredChargeOutcome;

/// <summary>
/// Handles <see cref="ApplyStoredChargeOutcomeCommand"/>: records how a stored method charge ended on its claim, and
/// when the money was taken, records the payment on the invoice in the same save.
/// </summary>
/// <remarks>
/// <para>
/// A reported success is believed only when it names a transaction and reports exactly the amount and currency the
/// claim asked for. Anything else, and a success for an invoice that can no longer take the payment, is a held claim:
/// the money is taken and sits with an operator, with a critical log line, and nothing is recorded on the invoice or
/// the client's balance on a guess. A success reported for less or more than what is still due (the invoice changed
/// while the charge was in flight) is recorded: a smaller amount as a part payment, a larger one as the payment plus a
/// credit of the surplus on the client's balance.
/// </para>
/// <para>
/// A claim that already ended is answered as it stands, so the charge and the reconciliation pass can both end here
/// for the same claim. A save that loses to another writer throws the conflict; nothing was recorded, the claim is
/// still unresolved and the reconciliation pass finds the charge again by its key.
/// </para>
/// </remarks>
public sealed class ApplyStoredChargeOutcomeHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Puts what the gateway took beyond the amount due on the client's credit balance.</summary>
    private readonly IClientCredit _clientCredit;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<ApplyStoredChargeOutcomeHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="clientCredit">Puts what the gateway took beyond the amount due on the client's credit balance.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    /// <param name="logger">Structured logger.</param>
    public ApplyStoredChargeOutcomeHandler(
        IHostpanelDbContext context,
        IClientCredit clientCredit,
        IClock clock,
        ILogger<ApplyStoredChargeOutcomeHandler> logger)
    {
        _context = context;
        _clientCredit = clientCredit;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The claim and what the plugin reported.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Where the charge stands after the outcome was applied; <c>INVOICE_PAYMENT_CLAIM_NOT_FOUND</c> when the id names
    /// no claim; or whatever the client's credit refuses with when a surplus could not be credited (nothing is saved).
    /// </returns>
    /// <exception cref="Innovayse.Hostpanel.Shared.Infrastructure.ConcurrencyConflictException">
    /// Another writer changed the claim or the invoice first; nothing was saved.
    /// </exception>
    public async Task<Result<InvoiceChargeDto>> HandleAsync(ApplyStoredChargeOutcomeCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var claim = await _context.InvoicePaymentClaims.FirstOrDefaultAsync(c => c.Id == cmd.ClaimId, ct);
        if (claim is null)
        {
            return Result<InvoiceChargeDto>.FailureCode(HostpanelErrorCodes.INVOICE_PAYMENT_CLAIM_NOT_FOUND);
        }

        // The charge and the reconciliation pass can both end here for one claim; the second one finds it ended.
        if (!claim.IsUnresolved)
        {
            return Result.Success(ToDto(claim));
        }

        // Stamped so the reconciliation pass backs off from a claim it has just looked at.
        claim.RecordCheck(_clock.UtcNow);

        switch (cmd.Outcome)
        {
            case null:
                claim.MarkAbandoned(_clock.UtcNow);
                break;
            case StoredChargeOutcome.Succeeded:
                return await SettleAsync(claim, cmd, ct);
            case StoredChargeOutcome.Declined:
                claim.MarkDeclined(_clock.UtcNow);
                break;
            case StoredChargeOutcome.ActionRequired:
                claim.MarkActionRequired(cmd.ActionUrl, _clock.UtcNow);
                break;
            case StoredChargeOutcome.Pending:
                claim.MarkPending();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(cmd), cmd.Outcome, "The outcome is not one the capability defines.");
        }

        await _context.SaveChangesAsync(ct);
        return Result.Success(ToDto(claim));
    }

    /// <summary>
    /// Records a reported success: checks it against the claim, records the payment on the invoice and ends the claim,
    /// or holds the claim when the money cannot be recorded.
    /// </summary>
    /// <param name="claim">The unresolved claim.</param>
    /// <param name="cmd">What the plugin reported.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Where the charge stands, or the refusal of the client's credit.</returns>
    private async Task<Result<InvoiceChargeDto>> SettleAsync(InvoicePaymentClaim claim, ApplyStoredChargeOutcomeCommand cmd, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var transactionId = cmd.TransactionId?.Trim();

        if (string.IsNullOrEmpty(transactionId) || !await MatchesClaimAsync(claim, cmd, ct))
        {
            _logger.LogCritical(
                "Stored charge claim {ClaimId} of invoice {InvoiceId} through {Module} reports success as {Amount} {Currency} with transaction {Transaction}, which is not what was asked ({Asked} minor units of {AskedCurrency}); held for an operator, nothing recorded.",
                claim.Id, claim.InvoiceId, claim.GatewayModuleId, cmd.AmountMinor, cmd.Currency, transactionId, claim.AmountMinor, claim.Currency);
            claim.MarkHeld(transactionId, now);
            await _context.SaveChangesAsync(ct);
            return Result.Success(ToDto(claim));
        }

        // Payments are loaded because the amount still due decides how the payment is recorded.
        var invoice = await _context.Invoices
            .Include(i => i.Transactions)
            .FirstOrDefaultAsync(i => i.Id == claim.InvoiceId, ct);
        if (invoice is null || !invoice.IsPayable)
        {
            _logger.LogCritical(
                "Stored charge claim {ClaimId} took money for invoice {InvoiceId} through {Module} as {Transaction}, but the invoice can no longer take a payment; held for an operator, nothing recorded.",
                claim.Id, claim.InvoiceId, claim.GatewayModuleId, transactionId);
            claim.MarkHeld(transactionId, now);
            await _context.SaveChangesAsync(ct);
            return Result.Success(ToDto(claim));
        }

        var credited = await RecordPaymentAsync(invoice, claim, transactionId, now, ct);
        if (credited.IsFailure)
        {
            return Result<InvoiceChargeDto>.FailureCode(credited.ErrorCode!);
        }

        claim.MarkSettled(transactionId, now);
        await _context.SaveChangesAsync(ct);
        return Result.Success(ToDto(claim));
    }

    /// <summary>
    /// Records the money the gateway took on the invoice. Exactly what is still due settles the invoice; less is a
    /// part payment; more settles it and puts the surplus on the client's balance.
    /// </summary>
    /// <param name="invoice">The payable invoice, with its payments loaded.</param>
    /// <param name="claim">The claim whose charge took the money.</param>
    /// <param name="transactionId">The gateway's transaction reference.</param>
    /// <param name="now">The current moment (UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or the refusal of the client's credit when a surplus could not be credited.</returns>
    private async Task<Result> RecordPaymentAsync(
        Invoice invoice, InvoicePaymentClaim claim, string transactionId, DateTimeOffset now, CancellationToken ct)
    {
        var dueMinor = CurrencyCode.ToMinorUnits(invoice.AmountDue, invoice.Currency);
        var paidMinor = claim.AmountMinor;

        if (paidMinor < dueMinor)
        {
            var part = CurrencyCode.FromMinorUnits(paidMinor, invoice.Currency);
            _context.Transactions.Add(invoice.AddPayment(
                now, claim.GatewayModuleId, transactionId, part, 0m, null, TransactionSource.GatewayPayment));
            return Result.Success();
        }

        var ledgerRow = invoice.Settle(claim.GatewayModuleId, transactionId, now, TransactionSource.GatewayPayment);
        if (ledgerRow is not null)
        {
            _context.Transactions.Add(ledgerRow);
        }

        // A credit top-up reaches the balance in the save that marks it paid.
        var toppedUp = await _clientCredit.CreditPaidTopUpAsync(invoice, ledgerRow, ct);
        if (toppedUp.IsFailure)
        {
            return toppedUp;
        }

        if (paidMinor == dueMinor)
        {
            return Result.Success();
        }

        // The charge was made for more than is due now: the rest belongs to the client and is kept on their balance.
        var surplus = CurrencyCode.FromMinorUnits(paidMinor - dueMinor, invoice.Currency);
        var credit = await _clientCredit.AddAsync(new ClientCreditChange(invoice.ClientId, surplus, CreditMovementType.PaymentSurplus, invoice.Id), ct);
        return credit.IsFailure ? Result.FailureCode(credit.ErrorCode!) : Result.Success();
    }

    /// <summary>Whether the amount and currency the plugin reported are exactly the ones the claim asked for.</summary>
    /// <param name="claim">The claim.</param>
    /// <param name="cmd">What the plugin reported.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when both match; a missing amount or currency does not match.</returns>
    private async Task<bool> MatchesClaimAsync(InvoicePaymentClaim claim, ApplyStoredChargeOutcomeCommand cmd, CancellationToken ct)
    {
        if (cmd.AmountMinor != claim.AmountMinor || string.IsNullOrEmpty(cmd.Currency))
        {
            return false;
        }

        var numeric = await _context.Currencies
            .AsNoTracking()
            .Where(c => c.Code == claim.Currency)
            .Select(c => c.Numeric)
            .FirstOrDefaultAsync(ct);
        return numeric is not null
            && int.TryParse(numeric, out var expected)
            && int.TryParse(cmd.Currency, out var reported)
            && expected == reported;
    }

    /// <summary>Maps a claim to the shape the callers get.</summary>
    /// <param name="claim">The claim.</param>
    /// <returns>Where the charge stands.</returns>
    private static InvoiceChargeDto ToDto(InvoicePaymentClaim claim) =>
        new(claim.Id, claim.InvoiceId, claim.Status, claim.Status == InvoicePaymentClaimStatus.ActionRequired ? claim.ActionUrl : null);
}
