using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.RenewDomain;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Services.Commands.ExtendServiceRenewal;
using Innovayse.Hostpanel.Application.Services.Commands.UnsuspendService;
using Innovayse.Hostpanel.Application.Services.Extensions;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Domain.Domains.Events;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ApplyPaidRenewals;

/// <summary>
/// Handles <see cref="ApplyPaidRenewalsCommand"/>: for a paid-in-full invoice, extends each renewed
/// service, renews each renewed domain at the registrar, and lifts overdue suspensions the payment
/// cleared. Every effect is saved before the next one is dispatched, so a crash part-way leaves
/// unapplied lines for the daily sweep, never a doubled extension.
/// </summary>
/// <remarks>
/// A domain line is claimed (<see cref="InvoiceItem.ClaimRenewal"/>, saved against the line's
/// renewal concurrency tokens) before the registrar is called, so a concurrent sweep, a lost save after a registrar
/// success, or a registrar timeout that actually renewed can never lead to a second paid period at
/// the registrar. A claimed-but-unapplied line is skipped by every runner; only a clean registrar
/// refusal releases the claim, an unknown outcome leaves it for a person.
/// </remarks>
/// <remarks>
/// Applying is one-way. <c>MarkUnpaid</c>, <c>Refund</c> and <c>Cancel</c> on an invoice whose
/// renewals were already applied do not roll the extension back (nor un-renew a domain at the
/// registrar): the service keeps its later <c>NextRenewalAt</c> until an operator adjusts the date by
/// hand. Re-paying such an invoice applies nothing twice, because the lines stay marked applied.
/// </remarks>
public sealed class ApplyPaidRenewalsHandler
{
    /// <summary>
    /// How far past the paid period's start a domain's expiry may sit and still be renewed by this
    /// line. Well under the shortest renewal (a year), well over any edit or normalisation nudge.
    /// </summary>
    private static readonly TimeSpan AlreadyRenewedSlack = TimeSpan.FromDays(30);

    /// <summary>The invoices and their renewal lines.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reaches the Services and Domains modules: their state, the extension, the registrar renewal, the unsuspend, and the staff alert.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<ApplyPaidRenewalsHandler> _logger;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The invoices and their renewal lines.</param>
    /// <param name="bus">Reaches the Services and Domains modules: their state, the extension, the registrar renewal, the unsuspend, and the staff alert.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    public ApplyPaidRenewalsHandler(IHostpanelDbContext context, IMessageBus bus, ILogger<ApplyPaidRenewalsHandler> logger, IClock clock)
    {
        _context = context;
        _bus = bus;
        _logger = logger;
        _clock = clock;
    }

    /// <summary>Applies the invoice's pending renewal lines.</summary>
    /// <param name="cmd">The invoice.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success, or <c>INVOICE_NOT_FOUND</c>.</returns>
    public async Task<Result> HandleAsync(ApplyPaidRenewalsCommand cmd, CancellationToken ct)
    {
        var invoice = await _context.Invoices
            .Include(i => i.Items)
            .Include(i => i.Transactions)
            .FirstOrDefaultAsync(i => i.Id == cmd.InvoiceId, ct);
        if (invoice is null)
        {
            return Result.FailureCode(HostpanelErrorCodes.INVOICE_NOT_FOUND);
        }

        if (!invoice.IsPaidInFull)
        {
            // Extending would give a whole period for part of the price. A partial payment keeps the
            // invoice open now, so a Paid invoice that is short is one marked Unpaid and paid again,
            // or older data; staff resolve it.
            if (invoice.IsPaid)
            {
                _logger.LogWarning(
                    "Invoice {InvoiceId} is {Status} but not paid in full; its renewals are left unapplied until staff resolve it.",
                    invoice.Id, invoice.Status);
            }

            return Result.Success();
        }

        // A line already claimed (RenewalAttemptAt set) but not applied belongs to another runner,
        // or to a person after an unknown registrar outcome: never touched here.
        foreach (var line in invoice.Items.Where(l => l.IsRenewal && l.RenewalAppliedAt is null && l.RenewalAttemptAt is null).ToList())
        {
            if (line.ClientServiceId is { } serviceId)
            {
                await ApplyServiceAsync(line, serviceId, ct);
            }
            else if (line.RegisteredDomainId is { } domainId)
            {
                await ApplyDomainAsync(invoice, line, domainId, ct);
            }
        }

        // Every service the invoice charged for, renewal or legacy line, may have been
        // suspended for this invoice being overdue; the legacy ones have no period to apply but
        // their suspension is still this payment's to lift.
        foreach (var serviceId in invoice.Items.Where(l => l.ClientServiceId is not null).Select(l => l.ClientServiceId!.Value).Distinct())
        {
            await LiftOverdueSuspensionAsync(invoice.Id, serviceId, ct);
        }

        return Result.Success();
    }

    /// <summary>
    /// Extends one service through the Services module, then marks its line applied. The extension is
    /// saved first, in its own scope: a crash before the marker lands leaves the line unapplied, and
    /// the sweep sends the same period again, which changes nothing because a renewal date only
    /// ever moves forward.
    /// </summary>
    /// <param name="line">The renewal line.</param>
    /// <param name="serviceId">The service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    private async Task ApplyServiceAsync(InvoiceItem line, int serviceId, CancellationToken ct)
    {
        var outcome = await _bus.InvokeAsync<Result>(new ExtendServiceRenewalCommand(serviceId, line.RenewalPeriodEnd!.Value), ct);
        if (outcome.IsFailure)
        {
            if (outcome.ErrorCode != HostpanelErrorCodes.SERVICE_NOT_FOUND)
            {
                // Nothing was extended; the line stays unapplied and the sweep retries it.
                _logger.LogWarning(
                    "Extending service {ServiceId} for renewal line {LineId} refused ({Code}); the sweep retries it.",
                    serviceId, line.Id, outcome.ErrorCode);
                return;
            }

            _logger.LogWarning("Renewal line {LineId} names service {ServiceId}, which no longer exists.", line.Id, serviceId);
        }

        // Marked even when the service is gone: there is nothing left to extend, and an unmarked
        // line would be retried by the sweep every day forever.
        line.MarkRenewalApplied(_clock.UtcNow);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (ConcurrencyConflictException)
        {
            // A service line has no claim, so the payment event and the sweep can both extend it;
            // the extension is idempotent, only the second marker save loses on the line's renewal
            // token. The line is applied either way. Reloading puts the tracked line back to what
            // the database holds, so the invoice's other lines and the unsuspend still run.
            await _context.ReloadAsync(line, ct);
            _logger.LogInformation("Renewal line {LineId} was marked applied by another runner; skipped.", line.Id);
        }
    }

    /// <summary>Renews one domain at the registrar and marks its line applied when that succeeded.</summary>
    /// <param name="invoice">The paid invoice.</param>
    /// <param name="line">The renewal line.</param>
    /// <param name="domainId">The domain.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    private async Task ApplyDomainAsync(Invoice invoice, InvoiceItem line, int domainId, CancellationToken ct)
    {
        var states = await _bus.GetDomainRenewalStatesAsync([domainId], ct);
        if (!states.TryGetValue(domainId, out var current))
        {
            await CloseWithoutEffectAsync(invoice, line, domainId, "(deleted)",
                "The domain no longer exists; the paid renewal was not applied. Review for a refund.", ct);
            return;
        }

        if (current.Status is DomainStatus.Cancelled or DomainStatus.Transferred)
        {
            // RegisteredDomain.Renew refuses these statuses by throwing; dispatching would fail the
            // whole command every day. Close the line and hand the refund question to staff.
            await CloseWithoutEffectAsync(invoice, line, domainId, current.Name,
                $"The domain is {current.Status} and cannot be renewed; the paid renewal was not applied. Review for a refund.", ct);
            return;
        }

        if (!current.IsAtRegistrar)
        {
            // Never placed at a registrar, so RenewDomainCommand refuses it every time; releasing the
            // claim would retry and alert daily for ever. Close the line and hand it to staff.
            await CloseWithoutEffectAsync(invoice, line, domainId, current.Name,
                "The domain is not linked to a registrar and cannot be renewed there; the paid renewal was not applied. Review for a refund.", ct);
            return;
        }

        // A real renewal moves the expiry by at least a year; an admin edit or the registrar sync
        // normalising the time of day nudges it by hours. Only a move past the slack counts as
        // "already renewed": a nudge alone must not leave a paid domain unrenewed.
        if (current.ExpiresAt > line.RenewalPeriodStart!.Value.Add(AlreadyRenewedSlack))
        {
            // Renewed some other way (a client renew order, staff) after this invoice was raised.
            // Renewing again would buy a second period with one payment's money; tell staff instead.
            // An expiry already at or past the period end most likely means this very invoice was
            // applied once and its marker lost: refunding would then be wrong.
            var reason = current.ExpiresAt >= line.RenewalPeriodEnd!.Value
                ? "Expiry already covers this period: likely already applied for this invoice; verify before refunding."
                : "Already renewed before this paid invoice was applied; review for a refund.";
            await CloseWithoutEffectAsync(invoice, line, domainId, current.Name, reason, ct);
            return;
        }

        if (!await ClaimAsync(line, ct))
        {
            _logger.LogInformation(
                "Renewal line {LineId} of invoice {InvoiceId} is already claimed by another runner; skipped.", line.Id, invoice.Id);
            return;
        }

        var years = Math.Max(1, line.RenewalPeriodEnd!.Value.Year - line.RenewalPeriodStart.Value.Year);
        Result outcome;
        try
        {
            outcome = await _bus.InvokeAsync<Result>(new RenewDomainCommand(domainId, years), ct);
        }
        catch (Exception ex)
        {
            // The registrar may or may not have renewed (a timeout is the usual case). Retrying
            // could buy a second period, so the claim stays and a person decides. Not rethrown:
            // the invoice's other lines still deserve to be applied.
            _logger.LogError(ex,
                "Paid renewal of domain {DomainId} (invoice {InvoiceId}, line {LineId}) ended with an unknown outcome; the line stays claimed.",
                domainId, invoice.Id, line.Id);
            await AlertAsync(domainId, invoice, current.Name,
                "Registrar outcome unknown. Verify at the registrar before retrying, this line will not be retried automatically.");
            return;
        }

        if (outcome.IsFailure)
        {
            // A clean refusal renewed nothing: release the claim so the daily sweep tries again,
            // and tell staff now.
            line.ReleaseRenewalClaim();
            await _context.SaveChangesAsync(ct);
            _logger.LogError("Paid renewal of domain {DomainId} refused: {Code}.", domainId, outcome.ErrorCode);
            await AlertAsync(domainId, invoice, current.Name, $"Registrar refused the paid renewal ({outcome.ErrorCode}). It is retried daily.");
            return;
        }

        // RenewDomainCommand saved the new expiry and due date in its own scope. Should that save be
        // lost, the line stays claimed and the stale-claim alert brings it to a person, as for any
        // registrar call whose outcome was not recorded.
        line.MarkRenewalApplied(_clock.UtcNow);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Claims a line before the registrar call. The save carries the line's renewal concurrency
    /// tokens, so it only lands while the line is still neither applied nor claimed in the
    /// database and exactly one runner ever wins it.
    /// </summary>
    /// <param name="line">The renewal line, read unapplied and unclaimed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Whether this runner now owns the line.</returns>
    private async Task<bool> ClaimAsync(InvoiceItem line, CancellationToken ct)
    {
        line.ClaimRenewal(_clock.UtcNow);
        try
        {
            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch (ConcurrencyConflictException)
        {
            // Another runner claimed or applied it first. Reloading puts the tracked line back to
            // what the database holds, so no later save in this handler retries the lost claim.
            await _context.ReloadAsync(line, ct);
            return false;
        }
    }

    /// <summary>
    /// Marks a domain line applied although nothing was renewed, logs it, and alerts staff: the
    /// warning is the record even where no staff mailbox is configured.
    /// </summary>
    /// <param name="invoice">The paid invoice.</param>
    /// <param name="line">The renewal line.</param>
    /// <param name="domainId">The domain.</param>
    /// <param name="name">The domain name.</param>
    /// <param name="reason">Why nothing was renewed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    private async Task CloseWithoutEffectAsync(Invoice invoice, InvoiceItem line, int domainId, string name, string reason, CancellationToken ct)
    {
        line.MarkRenewalApplied(_clock.UtcNow);
        await _context.SaveChangesAsync(ct);
        _logger.LogWarning(
            "Renewal line {LineId} of invoice {InvoiceId} for domain {DomainId} closed without renewing: {Reason}",
            line.Id, invoice.Id, domainId, reason);
        await AlertAsync(domainId, invoice, name, reason);
    }

    /// <summary>Unsuspends a service if this billing engine suspended it and nothing overdue still covers it.</summary>
    /// <param name="paidInvoiceId">The invoice just paid (excluded from the check).</param>
    /// <param name="serviceId">The service.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    private async Task LiftOverdueSuspensionAsync(int paidInvoiceId, int serviceId, CancellationToken ct)
    {
        var states = await _bus.GetServiceBillingStatesAsync([serviceId], ct);
        if (!states.TryGetValue(serviceId, out var service)
            || service is not { Status: ServiceStatus.Suspended, SuspensionSource: SuspensionSource.Overdue })
        {
            return;
        }

        var stillOverdue = await _context.Invoices.AnyAsync(
            i => i.Id != paidInvoiceId
                 && i.Status == InvoiceStatus.Overdue
                 && i.Items.Any(l => l.ClientServiceId == serviceId),
            ct);
        if (stillOverdue)
        {
            return;
        }

        var outcome = await _bus.InvokeAsync<Result>(new UnsuspendServiceCommand(serviceId), ct);
        if (outcome.IsFailure)
        {
            _logger.LogWarning("Unsuspend after payment refused for service {ServiceId} ({Code}).", serviceId, outcome.ErrorCode);
        }
    }

    /// <summary>Tells staff about a domain renewal that needs a person.</summary>
    /// <param name="domainId">The domain.</param>
    /// <param name="invoice">The paid invoice the domain line is on.</param>
    /// <param name="name">The domain name.</param>
    /// <param name="reason">What happened.</param>
    /// <returns>A task.</returns>
    private Task AlertAsync(int domainId, Invoice invoice, string name, string reason) =>
        _bus.PublishAsync(new DomainRenewalFailedEvent(domainId, invoice.ClientId, name, invoice.Id, reason) { OccurredAt = _clock.UtcNow }).AsTask();
}
