using System.Globalization;
using Innovayse.Hostpanel.Application.Billing.Commands.ApplyPaidRenewals;
using Innovayse.Hostpanel.Application.Billing.Commands.RaiseClientRenewalInvoices;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Clients.Extensions;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Extensions;
using Innovayse.Hostpanel.Application.Services.Commands.UnsuspendService;
using Innovayse.Hostpanel.Application.Services.Extensions;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Domains.Events;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;
namespace Innovayse.Hostpanel.Application.Billing.Commands.ProcessRenewalsCron;

/// <summary>
/// Handles <see cref="ProcessRenewalsCronCommand"/>, the daily renewal run. First any open renewal
/// invoice that owes nothing is marked paid (closing the gap between the two saves of a
/// credit-covered invoice). Then the sweep: every
/// paid invoice with a renewal line that is neither applied nor claimed gets an
/// <see cref="ApplyPaidRenewalsCommand"/> (so a lost payment event is applied within a day); every
/// service suspended for an overdue invoice that no Overdue invoice references any more is
/// unsuspended; staff are told on every run about a registrar claim that has stayed unconfirmed for a day or more; and
/// one Warning names auto-renewing services that have no renewal date. Then one
/// <see cref="RaiseClientRenewalInvoicesCommand"/> per candidate client that is not suspended or closed, each in its own message
/// scope. Nothing is extended here. Re-schedules itself for the next day through <see cref="RecurringJobs"/>.
/// </summary>
public sealed class ProcessRenewalsCronHandler
{
    /// <summary>How many service ids the undated-services warning names.</summary>
    private const int UndatedSampleSize = 10;

    /// <summary>The invoices the sweep, the heal and the overdue check read.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Message bus, for the per-invoice and per-client commands, the Services and Domains renewal queries, and staff alerts.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Runs the passes under their lease and schedules the next run.</summary>
    private readonly RecurringJobs _jobs;

    /// <summary>The lead time that bounds the candidate clients.</summary>
    private readonly RenewalSettingsReader _settings;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<ProcessRenewalsCronHandler> _logger;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The invoices the sweep, the heal and the overdue check read.</param>
    /// <param name="bus">Message bus, for the per-invoice and per-client commands, the Services and Domains renewal queries, and staff alerts.</param>
    /// <param name="jobs">Runs the passes under their lease and schedules the next run.</param>
    /// <param name="settings">The lead time that bounds the candidate clients.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    public ProcessRenewalsCronHandler(
        IHostpanelDbContext context,
        IMessageBus bus,
        RecurringJobs jobs,
        RenewalSettingsReader settings,
        ILogger<ProcessRenewalsCronHandler> logger,
        IClock clock)
    {
        _context = context;
        _bus = bus;
        _jobs = jobs;
        _settings = settings;
        _logger = logger;
        _clock = clock;
    }

    /// <summary>
    /// Sweeps paid-but-unapplied renewals, lifts stale overdue suspensions, alerts on stale claims,
    /// raises due renewal invoices, and always re-schedules.
    /// </summary>
    /// <param name="cmd">The scheduled command (marker, no payload).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    public Task HandleAsync(ProcessRenewalsCronCommand cmd, CancellationToken ct)
    {
        _ = cmd;
        var now = _clock.UtcNow;

        // Through RecurringJobs so the daily chain survives any failure (a throwing query, a dead
        // database); a missed day is not retried until tomorrow, but the job never silently stops.
        return _jobs.RunAsync<ProcessRenewalsCronCommand>(
            _jobs.Schedules.ProcessRenewals, () => RunAsync(now, ct), ct);
    }

    /// <summary>The run itself: heal, sweep, lift, alert, raise. Every step is isolated per item.</summary>
    /// <param name="now">The moment this pass started.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidOperationException">A renewable service or domain names a client that does not exist, so the database is broken.</exception>
    private async Task RunAsync(DateTimeOffset now, CancellationToken ct)
    {
        // Self-heal before the sweep: a credit-covered renewal invoice is saved, then marked paid in
        // a second save; a crash between the two leaves it open with nothing to pay. Two payments
        // saved together can also cover an invoice that neither settled. Paying it here puts its
        // lines in front of the sweep below.
        var healed = await PayOwedNothingInvoicesAsync(now, ct);

        // Sweep first: a paid renewal whose PaymentReceivedEvent was lost is applied here, within a
        // day, before anything new is raised. A claimed line (RenewalAttemptAt set) belongs to a
        // registrar call whose outcome a person must confirm; ApplyPaidRenewals would skip it, so
        // an invoice whose only pending lines are claimed is not dispatched at all.
        var unapplied = await SweepUnappliedRenewalsAsync(ct);

        var lifted = await LiftStaleOverdueSuspensionsAsync(ct);

        await AlertStaleClaimsAsync(now, ct);

        await WarnUndatedServicesAsync(ct);

        // Only the candidate clients are listed here; each RaiseClientRenewalInvoicesCommand applies
        // the full rules (lead time, duplicate guard, pricing) in its own scope. The horizon is the
        // configured lead time, read once per run, so a client with nothing due inside it is not
        // dispatched at all (each dispatch is a scope, a client load and a pricing pass).
        var settings = await _settings.ReadAsync(ct);
        var horizon = now.AddDays(settings.InvoiceDaysBefore);
        var services = await _bus.GetRenewableServicesAsync(null, horizon, ct);
        var domains = await _bus.GetRenewableDomainsAsync(null, horizon, ct);
        var clientIds = services.Select(s => s.ClientId)
            .Union(domains.Select(d => d.ClientId))
            .Order()
            .ToList();
        // A suspended or closed account is not billed: its renewals are left out here (the raise
        // command refuses them too) so the run does not log a refusal for it every day. The
        // restricting client keys on services and domains keep every owner alive.
        var accounts = await _bus.GetRequiredClientBriefsByIdAsync(clientIds, ct);
        var billable = clientIds
            .Where(id => accounts[id].Status.MayTransact())
            .ToList();
        foreach (var clientId in billable)
        {
            await DispatchAsync(
                $"RaiseClientRenewalInvoices(client {clientId})",
                () => _bus.InvokeAsync<Result<int>>(new RaiseClientRenewalInvoicesCommand(clientId), ct),
                ct);
        }

        _logger.LogInformation(
            "ProcessRenewalsCron paid {Healed} open invoices with nothing owed, swept {Swept} invoices, lifted {Lifted} suspensions and checked {Clients} clients.",
            healed, unapplied, lifted, billable.Count);
    }

    /// <summary>
    /// Dispatches an <see cref="ApplyPaidRenewalsCommand"/> for every paid invoice that still has a
    /// renewal line neither applied nor claimed, reading the invoice ids in keyset batches so a long
    /// backlog is never loaded at once and an invoice that fails is not read again this pass.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>How many invoices were dispatched.</returns>
    private async Task<int> SweepUnappliedRenewalsAsync(CancellationToken ct)
    {
        var swept = 0;
        var lastId = 0;
        while (true)
        {
            var invoiceIds = await _context.Invoices.AsNoTracking()
                .Where(i => i.Id > lastId
                            && Invoice.PaidStatuses.Contains(i.Status)
                            && i.Items.Any(l => l.RenewalPeriodStart != null && l.RenewalAppliedAt == null && l.RenewalAttemptAt == null))
                .OrderBy(i => i.Id)
                .Select(i => i.Id)
                .Take(JobBatch.Size)
                .ToListAsync(ct);
            if (invoiceIds.Count == 0)
            {
                return swept;
            }

            foreach (var invoiceId in invoiceIds)
            {
                await DispatchAsync(
                    $"ApplyPaidRenewals(invoice {invoiceId})",
                    () => _bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoiceId), ct),
                    ct);
            }

            swept += invoiceIds.Count;
            lastId = invoiceIds[^1];
        }
    }

    /// <summary>
    /// Unsuspends every service this billing engine suspended (<see cref="SuspensionSource.Overdue"/>)
    /// that no Overdue invoice references any more: the durable half of "payment lifts an overdue
    /// suspension". The payment path lifts it at once; this catches what that path cannot: a lost
    /// payment event on a legacy (non-renewal) invoice, an unsuspend that failed or was refused, an
    /// overdue invoice an operator cancelled, or a payment that landed while the suspend run was
    /// between its query and its suspension.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>How many services were unsuspended.</returns>
    private async Task<int> LiftStaleOverdueSuspensionsAsync(CancellationToken ct)
    {
        var suspended = await _bus.ListOverdueSuspendedServicesAsync(ct);
        if (suspended.Count == 0)
        {
            return 0;
        }

        var stillOverdue = await _context.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Overdue)
            .SelectMany(i => i.Items)
            .Where(l => l.ClientServiceId != null && suspended.Contains(l.ClientServiceId!.Value))
            .Select(l => l.ClientServiceId!.Value)
            .Distinct()
            .ToListAsync(ct);
        var serviceIds = suspended.Except(stillOverdue).ToList();

        var lifted = 0;
        foreach (var serviceId in serviceIds)
        {
            try
            {
                var outcome = await _bus.InvokeAsync<Result>(new UnsuspendServiceCommand(serviceId), ct);
                if (outcome.IsFailure)
                {
                    _logger.LogWarning("Lifting the overdue suspension of service {ServiceId} refused ({Code}); the next run retries it.", serviceId, outcome.ErrorCode);
                }
                else
                {
                    lifted++;
                    _logger.LogInformation("Service {ServiceId} was suspended for an overdue invoice that is no longer overdue; unsuspended.", serviceId);
                }
            }
            catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
            {
                _logger.LogError(ex, "Lifting the overdue suspension of service {ServiceId} failed; the next run retries it.", serviceId);
            }
        }

        return lifted;
    }

    /// <summary>
    /// Logs one Warning for the Active, auto-renewing services with a renewable cycle but no
    /// <c>NextRenewalAt</c>: nothing ever bills them, so they run unpaid with no other signal. One-off
    /// and free cycles legitimately have no renewal date and are not reported.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    private async Task WarnUndatedServicesAsync(CancellationToken ct)
    {
        var renewable = await _bus.ListUndatedRenewingServicesAsync(ct);
        if (renewable.Count == 0)
        {
            return;
        }

        // Bounded: one line per run, the first ids only, however many there are.
        _logger.LogWarning(
            "{Count} active auto-renewing services have no NextRenewalAt and are never billed; set their renewal date. First ids: {ServiceIds}",
            renewable.Count, string.Join(", ", renewable.Take(UndatedSampleSize)));
    }

    /// <summary>
    /// Marks paid every Unpaid or Overdue invoice that owes nothing and was left open: one that
    /// carries a renewal line and has a zero total, paid with the credit-balance method and no
    /// ledger row, as <c>RaiseClientRenewalInvoicesHandler</c> would have done had its second save
    /// landed; and one whose recorded payments cover its total, paid under the reference of its
    /// last payment. Two payments saved at the same moment each see only themselves, so together
    /// they can cover an invoice that neither one settled.
    /// </summary>
    /// <param name="now">The moment this pass started, stamped as the payment time.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>How many invoices were paid.</returns>
    private async Task<int> PayOwedNothingInvoicesAsync(DateTimeOffset now, CancellationToken ct)
    {
        var paid = 0;
        var lastId = 0;
        while (true)
        {
            // Tracked, not AsNoTracking: each invoice is marked paid and saved below. Keyset batches,
            // so a long backlog is never loaded at once and a failing invoice is not read again this pass.
            var invoices = await _context.Invoices
                .Where(Invoice.PayableFilter)
                .Where(i => i.Id > lastId
                            && ((i.Total == 0 && i.Items.Any(l => l.RenewalPeriodStart != null))
                                || (i.Total > 0
                                    && i.Transactions.Where(t => t.Type == InvoiceTransactionType.Payment).Sum(t => t.Amount) >= i.Total)))
                .OrderBy(i => i.Id)
                .Include(i => i.Transactions)
                .Take(JobBatch.Size)
                .ToListAsync(ct);
            if (invoices.Count == 0)
            {
                return paid;
            }

            foreach (var invoice in invoices)
            {
                try
                {
                    // Covered by its recorded payments: settled under the latest one's reference. Otherwise
                    // it is the zero-total renewal the credit balance paid.
                    if (!invoice.SettleIfCovered(now))
                    {
                        invoice.MarkPaid(PaymentMethodNames.CreditBalance, now);
                    }

                    await _context.SaveChangesAsync(ct);
                    paid++;
                    _logger.LogWarning("Invoice {InvoiceId} was open with nothing owed; marked paid.", invoice.Id);
                }
                catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
                {
                    // The failed change is still tracked and would ride along with, and fail, every later
                    // save in this loop; detaching it isolates this invoice, and the next run retries it.
                    _context.Invoices.Entry(invoice).State = EntityState.Detached;
                    _logger.LogError(ex, "Could not mark invoice {InvoiceId} paid; the next run retries it.", invoice.Id);
                }
            }

            lastId = invoices[^1].Id;
            _context.DetachAll();
        }
    }

    /// <summary>
    /// Tells staff about every domain renewal line claimed for a registrar call at least a day ago
    /// and still not applied: the call's outcome was never recorded (a timeout, or a save that
    /// failed after the registrar renewed) and nothing retries it. The alert repeats on every daily
    /// run, so once per claim per day, until a person resolves the line (applies or releases it);
    /// a single alert that was missed or lost would otherwise leave the paid renewal stuck silently.
    /// </summary>
    /// <param name="now">The moment this pass started.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    private async Task AlertStaleClaimsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var staleBefore = now.AddDays(-1);
        var lastInvoiceId = 0;
        while (true)
        {
            // Batched by invoice, so every stale line of an invoice is alerted in the same batch.
            var invoiceIds = await _context.Invoices.AsNoTracking()
                .Where(i => i.Id > lastInvoiceId
                            && i.Items.Any(l => l.RegisteredDomainId != null && l.RenewalAppliedAt == null
                                                && l.RenewalAttemptAt != null && l.RenewalAttemptAt <= staleBefore))
                .OrderBy(i => i.Id)
                .Select(i => i.Id)
                .Take(JobBatch.Size)
                .ToListAsync(ct);
            if (invoiceIds.Count == 0)
            {
                return;
            }

            var stale = await _context.Invoices.AsNoTracking()
                .Where(i => invoiceIds.Contains(i.Id))
                .SelectMany(i => i.Items
                    .Where(l => l.RegisteredDomainId != null && l.RenewalAppliedAt == null
                                && l.RenewalAttemptAt != null && l.RenewalAttemptAt <= staleBefore)
                    .Select(l => new { InvoiceId = i.Id, i.ClientId, DomainId = l.RegisteredDomainId!.Value, AttemptAt = l.RenewalAttemptAt!.Value }))
                .ToListAsync(ct);

            var domainIds = stale.Select(s => s.DomainId).Distinct().ToList();
            var states = await _bus.GetDomainRenewalStatesAsync(domainIds, ct);

            foreach (var line in stale)
            {
                var name = states.TryGetValue(line.DomainId, out var state) ? state.Name : "(deleted)";
                _logger.LogWarning(
                    "Renewal of domain {DomainId} (invoice {InvoiceId}) was attempted at {AttemptAt:u} and never confirmed.",
                    line.DomainId, line.InvoiceId, line.AttemptAt);
                await _bus.PublishAsync(new DomainRenewalFailedEvent(line.DomainId, line.ClientId, name, line.InvoiceId,
                    string.Create(CultureInfo.InvariantCulture,
                        $"Renewal attempted on {line.AttemptAt:yyyy-MM-dd HH:mm} UTC but not confirmed; verify at the registrar."))
                {
                    OccurredAt = now,
                });
            }

            lastInvoiceId = invoiceIds[^1];
        }
    }

    /// <summary>Dispatches one unit of work and logs, never rethrows, its failure, so one client cannot stop the run.</summary>
    /// <param name="label">What is dispatched, for the log.</param>
    /// <param name="invoke">Invokes the command and returns its result.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    /// <remarks>
    /// Invoked with the typed <c>InvokeAsync&lt;TResult&gt;</c>: through the untyped overload Wolverine
    /// treats a handler's return value as a cascading message, and a <see cref="Result"/> is not one.
    /// </remarks>
    private async Task DispatchAsync<TResult>(string label, Func<Task<TResult>> invoke, CancellationToken ct)
        where TResult : Result
    {
        try
        {
            var result = await invoke();
            if (result.IsFailure)
            {
                _logger.LogWarning("Renewal step {Step} refused ({Code}); the next run retries it.", label, result.ErrorCode);
            }
        }
        catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
        {
            _logger.LogError(ex, "Renewal step {Step} failed; the next run retries it.", label);
        }
    }
}
