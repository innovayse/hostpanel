using Innovayse.Hostpanel.Application.Billing.Commands.SettleInvoiceCardPayment;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.StoredCredentials;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// Hands out the exclusive right to charge one invoice's saved method, and settles what an unfinished attempt
/// left behind. Both the renewal job and the client's own payment take the right here before any money moves.
/// </summary>
/// <remarks>
/// The unique index on unfinished claims makes a second claim impossible at the database level; this class turns
/// that refusal into "busy". A claim whose time ran out is first looked up at the gateway by its idempotency key,
/// so a charge that went through is recorded instead of charged again, and a lookup that fails keeps the invoice
/// held rather than risk a second charge. <see cref="ReconcileStaleAsync"/> does the same lookup for every stale
/// claim without asking whether the invoice could still be charged, so a payment is recorded even if the invoice was
/// cancelled, paid another way or its agreement withdrawn in the meantime.
/// </remarks>
public sealed class InvoicePaymentClaimGate
{
    /// <summary>How long a claim counts as held when its holder never reports back.</summary>
    private static readonly TimeSpan ClaimLifetime = TimeSpan.FromMinutes(15);

    /// <summary>How many stale claims one pass reconciles, so a backlog is worked off over several passes.</summary>
    private const int PassBatchSize = 50;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The door to the module's stored credential capability.</summary>
    private readonly StoredCredentialGateway _gateway;

    /// <summary>Settles a charge found at the gateway.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The clock claim lifetimes are measured on.</summary>
    private readonly IClock _clock;

    /// <summary>Logs refused claims and reconciliations.</summary>
    private readonly ILogger<InvoicePaymentClaimGate> _logger;

    /// <summary>Initializes the gate.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="gateway">The door to the module's stored credential capability.</param>
    /// <param name="bus">Settles a charge found at the gateway.</param>
    /// <param name="clock">The clock claim lifetimes are measured on.</param>
    /// <param name="logger">Logs refused claims and reconciliations.</param>
    public InvoicePaymentClaimGate(
        IHostpanelDbContext context, StoredCredentialGateway gateway, IMessageBus bus, IClock clock, ILogger<InvoicePaymentClaimGate> logger)
    {
        _context = context;
        _gateway = gateway;
        _bus = bus;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Asks for the right to charge an invoice.</summary>
    /// <param name="invoice">The invoice, with its payments loaded.</param>
    /// <param name="gatewayModuleId">The payment gateway module the charge goes through.</param>
    /// <param name="kind">Who is paying.</param>
    /// <param name="gatewayIdempotencyKey">The key the gateway call of this claim is sent with.</param>
    /// <param name="customerRef">The client's account at the module, where a stale attempt is looked up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <see cref="InvoicePaymentClaimAcquisition.Acquired"/> with the claim; <see cref="InvoicePaymentClaimAcquisition.Busy"/>
    /// when another attempt holds the invoice or what a stale one did is not known; <see cref="InvoicePaymentClaimAcquisition.AlreadySettled"/>
    /// when a stale attempt had taken the money and it was settled just now.
    /// </returns>
    public async Task<InvoicePaymentClaimResult> AcquireAsync(
        Invoice invoice, string gatewayModuleId, InvoicePaymentClaimKind kind, string gatewayIdempotencyKey, string customerRef, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        var now = _clock.UtcNow;

        var held = await _context.InvoicePaymentClaims.FirstOrDefaultAsync(c => c.InvoiceId == invoice.Id && c.Outcome == null, ct);
        if (held is not null)
        {
            if (!held.IsStale(now))
            {
                return new InvoicePaymentClaimResult(InvoicePaymentClaimAcquisition.Busy, null);
            }

            // The holder never reported back: what it did at the gateway is looked up before anyone else may charge.
            switch (await ReconcileAsync(held, invoice, customerRef, ct))
            {
                case ClaimReconciliation.StillOpen:
                    return new InvoicePaymentClaimResult(InvoicePaymentClaimAcquisition.Busy, null);
                case ClaimReconciliation.Settled:
                    return new InvoicePaymentClaimResult(InvoicePaymentClaimAcquisition.AlreadySettled, null);
            }
        }

        var claim = InvoicePaymentClaim.Create(invoice.Id, gatewayModuleId, kind, gatewayIdempotencyKey, now, ClaimLifetime);
        _context.InvoicePaymentClaims.Add(claim);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // The unique index on unfinished claims, and only it: another request took the invoice between the
            // read and the insert. Any other database error is a real failure and is not hidden behind "busy".
            _context.DiscardPendingChanges();
            _logger.LogInformation("Another payment attempt took invoice {InvoiceId} first.", invoice.Id);
            return new InvoicePaymentClaimResult(InvoicePaymentClaimAcquisition.Busy, null);
        }

        return new InvoicePaymentClaimResult(InvoicePaymentClaimAcquisition.Acquired, claim);
    }

    /// <summary>Reconciles every stale unfinished claim, with no question asked about whether the invoice could still be charged.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>How many claims were looked at.</returns>
    public async Task<int> ReconcileStaleAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var stale = await _context.InvoicePaymentClaims
            .Where(c => c.Outcome == null && c.ExpiresAt <= now)
            .OrderBy(c => c.ExpiresAt)
            .Take(PassBatchSize)
            .ToListAsync(ct);

        foreach (var claim in stale)
        {
            var invoice = await _context.Invoices.Include(i => i.Transactions).FirstOrDefaultAsync(i => i.Id == claim.InvoiceId, ct);
            var customerRef = invoice is null ? null : await _context.FindGatewayCustomerRefAsync(invoice.ClientId, claim.GatewayModuleId, ct);
            if (invoice is null || customerRef is null)
            {
                // Nothing to look the charge up under: an operator has to find out whether money moved.
                _logger.LogError(
                    "The unfinished payment attempt {ClaimId} of invoice {InvoiceId} cannot be looked up at module {Module}: no invoice or no gateway account.",
                    claim.Id, claim.InvoiceId, claim.GatewayModuleId);
                continue;
            }

            if (await ReconcileAsync(claim, invoice, customerRef, ct) == ClaimReconciliation.StillOpen)
            {
                _logger.LogError(
                    "The unfinished payment attempt {ClaimId} of invoice {InvoiceId} could not be settled yet and needs attention if it stays.",
                    claim.Id, claim.InvoiceId);
            }
        }

        return stale.Count;
    }

    /// <summary>Looks an unfinished claim up at the gateway by its idempotency key and ends it when the answer is final.</summary>
    /// <param name="claim">The unfinished claim.</param>
    /// <param name="invoice">The claim's invoice, with its payments loaded.</param>
    /// <param name="customerRef">The client's account at the claim's module.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>What the lookup came to.</returns>
    public async Task<ClaimReconciliation> ReconcileAsync(InvoicePaymentClaim claim, Invoice invoice, string customerRef, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(invoice);

        var found = await _gateway.FindChargeAsync(claim.GatewayModuleId, customerRef, claim.GatewayIdempotencyKey, ct);
        if (found.IsFailure)
        {
            // What the attempt did is not known, and charging again could take the money twice.
            _logger.LogWarning(
                "Could not reconcile the unfinished payment attempt of invoice {InvoiceId}: {Code}", invoice.Id, found.ErrorCode);
            return ClaimReconciliation.StillOpen;
        }

        var lookup = found.Value!;
        if (!lookup.Found)
        {
            // Trusted only because the lookup itself succeeded: the gateway has no charge under this key.
            return await EndAsync(claim, ClaimReconciliation.Closed, InvoicePaymentClaimOutcome.Expired, ct);
        }

        switch (lookup.Outcome)
        {
            case StoredChargeOutcome.Succeeded:
                return await SettleFoundChargeAsync(claim, invoice, lookup, ct);
            case StoredChargeOutcome.Declined:
            case StoredChargeOutcome.ActionRequired:
                return await EndAsync(claim, ClaimReconciliation.Closed, InvoicePaymentClaimOutcome.Failed, ct);
            default:
                // Pending, or an outcome this host does not know: no money may be assumed either way.
                return ClaimReconciliation.StillOpen;
        }
    }

    /// <summary>Records a charge the gateway reports as taken, and ends the claim only when the money is accounted for.</summary>
    /// <param name="claim">The unfinished claim.</param>
    /// <param name="invoice">The claim's invoice.</param>
    /// <param name="lookup">What the gateway reported for the key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Settled, closed when the money was handed back, otherwise still open.</returns>
    private async Task<ClaimReconciliation> SettleFoundChargeAsync(
        InvoicePaymentClaim claim, Invoice invoice, StoredChargeLookup lookup, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(lookup.TransactionId) || lookup.AmountMinor is not { } amountMinor || string.IsNullOrEmpty(lookup.Currency))
        {
            _logger.LogError(
                "The payment attempt of invoice {InvoiceId} took money but the gateway reported no reference, amount or currency for it.", invoice.Id);
            return ClaimReconciliation.StillOpen;
        }

        var settled = await _bus.InvokeAsync<Result>(
            new SettleInvoiceCardPaymentCommand(invoice.Id, lookup.TransactionId, CurrencyCode.FromMinorUnits(amountMinor, lookup.Currency), lookup.Currency), ct);
        if (settled.IsSuccess)
        {
            return await EndAsync(claim, ClaimReconciliation.Settled, InvoicePaymentClaimOutcome.Settled, ct);
        }

        // Settlement refuses by handing the money back. Only when the ledger shows that was done is the attempt over;
        // money that is held for an operator, or not accounted for at all, keeps the invoice held.
        if (await _context.GetReturnStateAsync(lookup.TransactionId, ct) == CardReturnState.Returned)
        {
            return await EndAsync(claim, ClaimReconciliation.Closed, InvoicePaymentClaimOutcome.Failed, ct);
        }

        _logger.LogCritical(
            "The payment attempt of invoice {InvoiceId} took money ({TransactionId}) that could not be recorded or handed back: {Code}.",
            invoice.Id, lookup.TransactionId, settled.ErrorCode);
        return ClaimReconciliation.StillOpen;
    }

    /// <summary>Ends a claim and saves.</summary>
    /// <param name="claim">The claim.</param>
    /// <param name="result">What to report.</param>
    /// <param name="outcome">How the claim ended.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><paramref name="result"/>.</returns>
    private async Task<ClaimReconciliation> EndAsync(
        InvoicePaymentClaim claim, ClaimReconciliation result, InvoicePaymentClaimOutcome outcome, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        switch (outcome)
        {
            case InvoicePaymentClaimOutcome.Settled:
                claim.MarkSettled(now);
                break;
            case InvoicePaymentClaimOutcome.Failed:
                claim.MarkFailed(now);
                break;
            default:
                claim.MarkExpired(now);
                break;
        }

        await _context.SaveChangesAsync(ct);
        return result;
    }
}
