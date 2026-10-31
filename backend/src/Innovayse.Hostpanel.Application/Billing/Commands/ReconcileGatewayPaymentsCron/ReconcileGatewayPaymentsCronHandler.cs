using Innovayse.Hostpanel.Application.Billing.Commands.CompleteGatewayPayment;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;
namespace Innovayse.Hostpanel.Application.Billing.Commands.ReconcileGatewayPaymentsCron;

/// <summary>
/// Handles <see cref="ReconcileGatewayPaymentsCronCommand"/>: asks the gateway about every unpaid
/// invoice whose hosted session is old enough to have left the payment page, with a growing
/// back-off between two asks about the same session, until the gateway gives a final answer; then
/// reschedules itself.
/// </summary>
/// <remarks>
/// <para>
/// The back-off (<c>ScheduledJobsOptions</c>) starts at the first ask once the session is old enough
/// (hosted sessions last 20 minutes, so a younger one may still be on the payment page), doubles
/// after every ask and stops growing at its upper bound. A final answer settles the payment through
/// <see cref="CompleteGatewayPaymentCommand"/>, which does it once however often it is asked, or
/// concludes a declined session, which is then left alone. A payment still pending after the
/// stalled threshold tells staff once per session.
/// </para>
/// <para>
/// A settled invoice whose kept session no payment was recorded through is swept the same way for
/// the first stalled-threshold hours only: nothing is unresolved on it, the sweep just recovers money
/// a session took after another path settled the invoice.
/// </para>
/// <para>
/// One failing gateway call is logged and counted as an ask; it never stops the pass or the schedule.
/// </para>
/// </remarks>
public sealed class ReconcileGatewayPaymentsCronHandler
{
    /// <summary>The module's data context, for the invoices with a pending gateway session.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Wolverine message bus.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Runs the passes under their lease and schedules the next run.</summary>
    private readonly RecurringJobs _jobs;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<ReconcileGatewayPaymentsCronHandler> _logger;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context, for the invoices with a pending gateway session.</param>
    /// <param name="bus">Wolverine message bus.</param>
    /// <param name="jobs">Runs the passes under their lease and schedules the next run.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    public ReconcileGatewayPaymentsCronHandler(
        IHostpanelDbContext context,
        IMessageBus bus,
        RecurringJobs jobs,
        ILogger<ReconcileGatewayPaymentsCronHandler> logger,
        IClock clock)
    {
        _context = context;
        _bus = bus;
        _jobs = jobs;
        _logger = logger;
        _clock = clock;
    }

    /// <summary>Handles the cron command.</summary>
    /// <param name="cmd">The cron command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when all due sessions were checked.</returns>
    public Task HandleAsync(ReconcileGatewayPaymentsCronCommand cmd, CancellationToken ct)
    {
        _ = cmd;

        // The reconciler is the only safety net for the whole no-webhook payment design: if the
        // query itself throws (DB blip, etc.), the next run must still be scheduled, or every
        // unpaid gateway session past this point silently stops being checked until a restart.
        // The next run is scheduled from the end of this one, so runs never overlap.
        var startedAt = _clock.UtcNow;
        return _jobs.RunAsync<ReconcileGatewayPaymentsCronCommand>(
            _jobs.Schedules.ReconcileGatewayPayments, () => RunAsync(startedAt, ct), ct);
    }

    /// <summary>One pass of the job; <see cref="HandleAsync"/> runs it through <see cref="RecurringJobs"/>.</summary>
    /// <param name="now">When this pass started.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    private async Task RunAsync(DateTimeOffset now, CancellationToken ct)
    {
        var schedules = _jobs.Schedules;
        var startedBefore = now - schedules.GatewaySessionSettle;
        var sweptAfter = now - schedules.GatewayStalledAfter;

        // Read-only here; the dispatched command below reloads and mutates the invoice in its own scope.
        var open = _context.Invoices
            .AsNoTracking()
            .Where(i => i.GatewayOrderId != null
                && i.GatewayConcludedAt == null
                && i.GatewayStartedAt < startedBefore);

        // Settled invoices whose kept session no payment was recorded through go the same way: the
        // completion asks the gateway and credits what the session took, once. Once a payment is
        // recorded through the session's module, or the session is older than the stalled threshold,
        // they drop out.
        var payable = await open
            .Where(Invoice.PayableFilter)
            .Select(i => new GatewaySessionCheck(
                i.Id, i.ClientId, i.GatewayModule!, i.GatewayStartedAt!.Value, i.GatewayCheckedAt, i.GatewayCheckCount, false))
            .ToListAsync(ct);
        var settled = await open
            .Where(i => i.GatewayStartedAt > sweptAfter)
            .Where(Invoice.SettledWithUnrecordedSessionFilter)
            .Select(i => new GatewaySessionCheck(
                i.Id, i.ClientId, i.GatewayModule!, i.GatewayStartedAt!.Value, i.GatewayCheckedAt, i.GatewayCheckCount, true))
            .ToListAsync(ct);

        var due = payable.Concat(settled).Where(session => IsDue(session, now)).ToList();

        _logger.LogInformation("Gateway payment reconciliation: {Count} session(s) due.", due.Count);

        foreach (var session in due)
        {
            var concluded = await CompleteAsync(session, ct);
            await RecordCheckAsync(session, concluded, now, ct);
        }
    }

    /// <summary>Whether the back-off since the last ask about a session has run out.</summary>
    /// <param name="session">The pending gateway session and when it was last asked about.</param>
    /// <param name="now">When this pass started.</param>
    /// <returns><see langword="true"/> when the session was never asked about, or its wait has passed.</returns>
    private bool IsDue(GatewaySessionCheck session, DateTimeOffset now) =>
        session.CheckedAt is not { } checkedAt
        || checkedAt + _jobs.Schedules.GatewayCheckInterval(session.CheckCount) <= now;

    /// <summary>
    /// Asks the gateway about one session through the completion command, which settles a paid
    /// payment exactly once. A refusal or a throw is logged: one bad session must not starve the
    /// rest, and it is asked about again after its next back-off.
    /// </summary>
    /// <param name="session">The session to complete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the gateway declined the session for good.</returns>
    private async Task<bool> CompleteAsync(GatewaySessionCheck session, CancellationToken ct)
    {
        try
        {
            var result = await _bus.InvokeAsync<Result<GatewayCompletionDto>>(
                new CompleteGatewayPaymentCommand(session.InvoiceId), ct);
            if (result.IsFailure)
            {
                // Logged rather than thrown: the failure already arrived as a Result, and wrapping a
                // code back into an exception just to reach the catch below would be
                // exception-as-control-flow for no gain.
                _logger.LogError(
                    "Reconciliation failed for invoice {InvoiceId}: {Code}.", session.InvoiceId, result.ErrorCode);
                return false;
            }

            if (result.Value!.State == GatewayCompletionState.Paid)
            {
                _logger.LogWarning(
                    "Reconciler recovered a paid-but-unreturned payment for invoice {InvoiceId}.", session.InvoiceId);
            }

            return result.Value.State == GatewayCompletionState.Declined;
        }
        catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
        {
            _logger.LogError(ex, "Reconciliation failed for invoice {InvoiceId}.", session.InvoiceId);
            return false;
        }
    }

    /// <summary>
    /// Records the ask on the invoice (for the back-off, and to leave a concluded session alone) and,
    /// when the payment has stayed pending past the stalled threshold, tells staff once per session.
    /// A failure to record is logged and never stops the pass.
    /// </summary>
    /// <param name="session">The session that was asked about.</param>
    /// <param name="concluded">Whether the gateway declined the session for good.</param>
    /// <param name="now">When this pass started.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    private async Task RecordCheckAsync(GatewaySessionCheck session, bool concluded, DateTimeOffset now, CancellationToken ct)
    {
        try
        {
            // Tracked and read after the completion saved in its own scope, so this sees the invoice as it is now.
            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.Id == session.InvoiceId, ct);
            if (invoice is null)
            {
                return;
            }

            invoice.RecordGatewayCheck(now, concluded);

            var pendingFor = now - session.StartedAt;
            var stalled = !concluded
                && invoice.IsPayable
                && invoice.GatewayStalledNotifiedAt is null
                && pendingFor >= _jobs.Schedules.GatewayStalledAfter;
            if (stalled)
            {
                invoice.MarkGatewayStalledNotified(now);
            }

            // Saved before staff are told, so a failed save repeats the ask instead of alerting about an unsaved result.
            await _context.SaveChangesAsync(ct);

            if (stalled)
            {
                await _bus.PublishAsync(new GatewayPaymentStalledEvent(
                    invoice.Id, invoice.ClientId, session.Module, (int)pendingFor.TotalHours) { OccurredAt = now });
            }
        }
        catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
        {
            _context.DetachAll();
            _logger.LogError(ex, "Reconciliation could not record the check of invoice {InvoiceId}.", session.InvoiceId);
        }
    }
}
