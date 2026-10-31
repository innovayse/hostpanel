using Innovayse.Hostpanel.Application.Billing.Commands.SettleInvoiceCardPayment;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// Hands out the exclusive right to charge an invoice's card, so the renewal job and the client can never
/// charge the same invoice at the same moment.
/// </summary>
/// <remarks>
/// The database decides: a unique index allows one unfinished claim per invoice, so of two parallel
/// requests exactly one inserts its claim. A claim whose holder never reported back is reconciled with the
/// gateway before another may start, so an attempt that took the money but lost the answer is settled, never charged again.
/// </remarks>
public sealed class InvoicePaymentClaimGate
{
    /// <summary>How long a claim counts as held when its holder never reports back.</summary>
    private static readonly TimeSpan ClaimLifetime = TimeSpan.FromMinutes(15);

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The card gateway, asked what a stale attempt did.</summary>
    private readonly ICardPaymentGateway _cardGateway;

    /// <summary>Message bus, for settling an invoice a stale attempt had already paid.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The clock every moment in this class is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<InvoicePaymentClaimGate> _logger;

    /// <summary>Initializes the gate.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="cardGateway">The card gateway, asked what a stale attempt did.</param>
    /// <param name="bus">Message bus, for settling an invoice a stale attempt had already paid.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    /// <param name="logger">Structured logger.</param>
    public InvoicePaymentClaimGate(
        IHostpanelDbContext context, ICardPaymentGateway cardGateway, IMessageBus bus, IClock clock, ILogger<InvoicePaymentClaimGate> logger)
    {
        _context = context;
        _cardGateway = cardGateway;
        _bus = bus;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>Asks for the right to charge an invoice.</summary>
    /// <param name="invoice">The invoice to charge.</param>
    /// <param name="kind">Who is charging.</param>
    /// <param name="gatewayIdempotencyKey">The key the charge is sent with.</param>
    /// <param name="customerId">The card gateway customer the invoice's client pays through, used to look up a stale attempt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The claim when it was acquired; <see cref="InvoicePaymentClaimAcquisition.Busy"/> when another
    /// attempt holds the invoice or what a stale one did could not be found out;
    /// <see cref="InvoicePaymentClaimAcquisition.AlreadySettled"/> when a stale attempt had taken the money.
    /// </returns>
    public async Task<InvoicePaymentClaimResult> AcquireAsync(
        Invoice invoice, InvoicePaymentClaimKind kind, string gatewayIdempotencyKey, string customerId, CancellationToken ct)
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

            var reconciled = await ReconcileStaleAsync(held, invoice, customerId, now, ct);
            if (reconciled is { } early)
            {
                return early;
            }
        }

        var claim = InvoicePaymentClaim.Start(invoice.Id, kind, gatewayIdempotencyKey, now, ClaimLifetime);
        _context.InvoicePaymentClaims.Add(claim);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The unique index on unfinished claims: another request took the invoice between the read and the insert.
            _context.DiscardPendingChanges();
            _logger.LogInformation("Another payment attempt took invoice {InvoiceId} first.", invoice.Id);
            return new InvoicePaymentClaimResult(InvoicePaymentClaimAcquisition.Busy, null);
        }

        return new InvoicePaymentClaimResult(InvoicePaymentClaimAcquisition.Acquired, claim);
    }

    /// <summary>
    /// Finds out what a claim whose time ran out did at the gateway: settles the invoice when the attempt
    /// took the money, frees the invoice when it took none.
    /// </summary>
    /// <param name="held">The stale claim.</param>
    /// <param name="invoice">The invoice it was for.</param>
    /// <param name="customerId">The card gateway customer the attempt charged.</param>
    /// <param name="now">The current moment.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The answer to hand back when the invoice must not be charged now (busy, or already settled);
    /// <see langword="null"/> when the stale claim took no money, is marked expired, and a new claim may start.
    /// </returns>
    private async Task<InvoicePaymentClaimResult?> ReconcileStaleAsync(
        InvoicePaymentClaim held, Invoice invoice, string customerId, DateTimeOffset now, CancellationToken ct)
    {
        var found = await _cardGateway.FindChargeAsync(
            new CardChargeLookupInstruction(customerId, invoice.Id, held.GatewayIdempotencyKey), ct);
        if (found.Failure is not null)
        {
            // What the attempt did is not known, and charging again could take the money twice.
            _logger.LogWarning("Could not reconcile the stale payment attempt of invoice {InvoiceId}: {Failure}", invoice.Id, found.Failure);
            return new InvoicePaymentClaimResult(InvoicePaymentClaimAcquisition.Busy, null);
        }

        if (found.Found && found.TransactionId is { Length: > 0 } transactionId)
        {
            var settled = await _bus.InvokeAsync<Result>(
                new SettleInvoiceCardPaymentCommand(invoice.Id, transactionId, found.Amount, found.Currency), ct);
            if (settled.IsFailure)
            {
                _logger.LogError(
                    "The stale attempt of invoice {InvoiceId} took the money but it could not be settled: {Code}", invoice.Id, settled.ErrorCode);
                return new InvoicePaymentClaimResult(InvoicePaymentClaimAcquisition.Busy, null);
            }

            held.MarkSettled(now);
            await _context.SaveChangesAsync(ct);
            return new InvoicePaymentClaimResult(InvoicePaymentClaimAcquisition.AlreadySettled, null);
        }

        held.MarkExpired(now);
        return null;
    }
}
