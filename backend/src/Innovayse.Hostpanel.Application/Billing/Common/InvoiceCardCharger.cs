using Innovayse.Hostpanel.Application.Billing.Commands.SettleInvoiceCardPayment;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.StoredCredentials;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// Charges an invoice's saved method once the caller holds the invoice's <see cref="InvoicePaymentClaim"/>, and
/// settles or ends the claim by what the gateway answers. The renewal job and the client's own payment share it,
/// so both treat money the same way.
/// </summary>
/// <remarks>
/// The claim is ended only when the outcome is final and the money is accounted for: settled, declined, action
/// required, or taken but handed back. When the gateway did not answer, answered "pending", or took the money
/// that could not be recorded, the claim stays held, so no second charge can start and the reconciliation pass
/// settles it later from the gateway's own record.
/// </remarks>
public sealed class InvoiceCardCharger
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The door to the module's stored credential capability.</summary>
    private readonly StoredCredentialGateway _gateway;

    /// <summary>Settles the charge on the invoice.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The clock claims are ended on.</summary>
    private readonly IClock _clock;

    /// <summary>Logs outcomes that need attention.</summary>
    private readonly ILogger<InvoiceCardCharger> _logger;

    /// <summary>Initializes the charger.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="gateway">The door to the module's stored credential capability.</param>
    /// <param name="bus">Settles the charge on the invoice.</param>
    /// <param name="clock">The clock claims are ended on.</param>
    /// <param name="logger">Logs outcomes that need attention.</param>
    public InvoiceCardCharger(
        IHostpanelDbContext context, StoredCredentialGateway gateway, IMessageBus bus, IClock clock, ILogger<InvoiceCardCharger> logger)
    {
        _context = context;
        _gateway = gateway;
        _bus = bus;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Charges what the invoice still owes to a saved method, under the claim's idempotency key.</summary>
    /// <param name="invoice">The invoice, with its payments loaded and its amount due read after the claim was taken.</param>
    /// <param name="claim">The held claim.</param>
    /// <param name="customerRef">The client's account at the claim's module.</param>
    /// <param name="methodRef">The saved method to charge.</param>
    /// <param name="returnUrl">Where a payer who has to authenticate is sent.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>How the charge ended.</returns>
    public async Task<InvoiceChargeResult> ChargeAsync(
        Invoice invoice, InvoicePaymentClaim claim, string customerRef, string methodRef, string returnUrl, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(claim);

        var amountMinor = CurrencyCode.ToMinorUnits(invoice.AmountDue, invoice.Currency);
        var charged = await _gateway.ChargeAsync(
            claim.GatewayModuleId,
            new StoredChargeInstruction(
                customerRef, methodRef, amountMinor, invoice.Currency, invoice.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                claim.GatewayIdempotencyKey, returnUrl),
            ct);
        if (charged.IsFailure)
        {
            // Nothing is known about the money: the claim stays held and is looked up before any new attempt.
            _logger.LogWarning("The gateway gave no answer while charging invoice {InvoiceId}: {Code}", invoice.Id, charged.ErrorCode);
            return new InvoiceChargeResult(InvoiceChargeOutcome.Unavailable);
        }

        var result = charged.Value!;
        switch (result.Outcome)
        {
            case StoredChargeOutcome.Succeeded:
                return await SettleAsync(invoice, claim, result, amountMinor, ct);
            case StoredChargeOutcome.Declined:
                return await EndFailedAsync(claim, InvoiceChargeOutcome.Declined, ct);
            case StoredChargeOutcome.ActionRequired:
                return await EndFailedAsync(claim, InvoiceChargeOutcome.ActionRequired, ct);
            case StoredChargeOutcome.Pending:
                return new InvoiceChargeResult(InvoiceChargeOutcome.Pending);
            default:
                // An outcome this host does not know is never read as paid or as refused.
                _logger.LogError("The gateway answered an unknown charge outcome {Outcome} for invoice {InvoiceId}.", result.Outcome, invoice.Id);
                return new InvoiceChargeResult(InvoiceChargeOutcome.Unavailable);
        }
    }

    /// <summary>Records a charge the gateway reports as taken.</summary>
    /// <param name="invoice">The invoice.</param>
    /// <param name="claim">The held claim.</param>
    /// <param name="result">What the gateway answered.</param>
    /// <param name="expectedMinor">The amount that was asked for, in minor units.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Settled, or not recorded with the reason.</returns>
    private async Task<InvoiceChargeResult> SettleAsync(
        Invoice invoice, InvoicePaymentClaim claim, StoredChargeResult result, long expectedMinor, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(result.TransactionId))
        {
            // The money may be taken but there is nothing to record it under: the claim stays held and the next
            // look-up finds the charge by its key.
            _logger.LogError("The gateway reported invoice {InvoiceId} charged but gave no reference.", invoice.Id);
            return new InvoiceChargeResult(InvoiceChargeOutcome.NotRecorded, HostpanelErrorCodes.GATEWAY_TRANSACTION_MISSING_UPSTREAM_FAILED);
        }

        if (result.AmountMinor != expectedMinor
            || !string.Equals(result.Currency, invoice.Currency, StringComparison.OrdinalIgnoreCase))
        {
            // The gateway took a different amount than was asked for (or did not say): nothing is recorded on the
            // word of a figure that does not match, and the look-up settles or returns it.
            _logger.LogError(
                "The gateway took {TakenMinor} {TakenCurrency} for invoice {InvoiceId} but {ExpectedMinor} {Currency} was asked.",
                result.AmountMinor, result.Currency, invoice.Id, expectedMinor, invoice.Currency);
            return new InvoiceChargeResult(InvoiceChargeOutcome.NotRecorded, HostpanelErrorCodes.GATEWAY_CHARGE_UPSTREAM_FAILED);
        }

        var settled = await _bus.InvokeAsync<Result>(
            new SettleInvoiceCardPaymentCommand(invoice.Id, result.TransactionId, invoice.AmountDue, invoice.Currency), ct);
        if (settled.IsSuccess)
        {
            claim.MarkSettled(_clock.UtcNow);
            await _context.SaveChangesAsync(ct);
            return new InvoiceChargeResult(InvoiceChargeOutcome.Settled);
        }

        // Settlement refuses by handing the money back (refund, balance) or holding it for an operator. The claim
        // ends only when the ledger shows the money was handed back; otherwise it stays held, so the client cannot
        // be charged a second time while the first charge is unaccounted for.
        if (await _context.GetReturnStateAsync(result.TransactionId, ct) == CardReturnState.Returned)
        {
            claim.MarkFailed(_clock.UtcNow);
            await _context.SaveChangesAsync(ct);
        }
        else
        {
            _logger.LogCritical(
                "Invoice {InvoiceId} was charged ({TransactionId}) but the payment could not be recorded or handed back: {Code}.",
                invoice.Id, result.TransactionId, settled.ErrorCode);
        }

        return new InvoiceChargeResult(InvoiceChargeOutcome.NotRecorded, settled.ErrorCode);
    }

    /// <summary>Ends the claim of an attempt that took no money.</summary>
    /// <param name="claim">The held claim.</param>
    /// <param name="outcome">How the charge ended.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The outcome.</returns>
    private async Task<InvoiceChargeResult> EndFailedAsync(InvoicePaymentClaim claim, InvoiceChargeOutcome outcome, CancellationToken ct)
    {
        claim.MarkFailed(_clock.UtcNow);
        await _context.SaveChangesAsync(ct);
        return new InvoiceChargeResult(outcome);
    }
}
