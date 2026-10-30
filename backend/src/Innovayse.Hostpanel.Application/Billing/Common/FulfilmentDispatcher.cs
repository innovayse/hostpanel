using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Settings;
using Innovayse.SDK.Plugins.Fulfilment;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Application.Billing.Common;

/// <summary>
/// One pass of the fulfilment dispatch: takes the <see cref="FulfilmentRecord"/>s that are due, one at a
/// time, tells the plugin each names what happened to the invoice, and records whether it accepted.
/// The scheduled chain and the pass started after a payment both run it, so what a pass does is written once.
/// </summary>
/// <remarks>
/// <para>
/// Delivery is at least once. A record is claimed and saved before the plugin is called, so of two
/// dispatchers that read the same record only one wins the save; a dispatcher that dies after the claim
/// leaves the record to the next one once the lease has run out, which counts the silent attempt as
/// failed. A plugin may therefore hear about an invoice twice and keys its work on the invoice id.
/// </para>
/// <para>
/// One plugin hears paid, refunded and lapsed in that order: a record waits while a record of an earlier
/// kind for the same invoice and plugin is not done, a failed one included, since a refund of something
/// never delivered would take back nothing; staff retry the earlier record and the later one follows.
/// A record whose plugin is not enabled is left alone, with no attempt spent, until the plugin is.
/// </para>
/// <para>
/// A plugin that throws or does not answer in time fails only its own attempt: the error is recorded
/// for staff (never shown to a client) and the pass goes on to the next record.
/// </para>
/// </remarks>
public sealed class FulfilmentDispatcher
{
    /// <summary>The data context the records and invoices are read and saved through.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the external account a client is linked to.</summary>
    private readonly IClientExternalAccounts _accounts;

    /// <summary>Resolves the fulfilment plugins an admin has switched on.</summary>
    private readonly IFulfilmentPluginResolver _plugins;

    /// <summary>The timings of the dispatch: batch size, call timeout and claim lease.</summary>
    private readonly IOptions<ScheduledJobsOptions> _options;

    /// <summary>The clock every moment in a pass is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<FulfilmentDispatcher> _logger;

    /// <summary>Initializes a new instance of the <see cref="FulfilmentDispatcher"/> class.</summary>
    /// <param name="context">The data context the records and invoices are read and saved through.</param>
    /// <param name="accounts">Reads the external account a client is linked to.</param>
    /// <param name="plugins">Resolves the fulfilment plugins an admin has switched on.</param>
    /// <param name="options">The timings of the dispatch: batch size, call timeout and claim lease.</param>
    /// <param name="clock">The clock every moment in a pass is read from.</param>
    /// <param name="logger">Structured logger.</param>
    public FulfilmentDispatcher(
        IHostpanelDbContext context,
        IClientExternalAccounts accounts,
        IFulfilmentPluginResolver plugins,
        IOptions<ScheduledJobsOptions> options,
        IClock clock,
        ILogger<FulfilmentDispatcher> logger)
    {
        _context = context;
        _accounts = accounts;
        _plugins = plugins;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Runs one pass: delivers due records, at most one batch, until none is left that may go.
    /// A failing delivery is recorded on its record and never stops the pass; only a cancellation of
    /// <paramref name="ct"/> (the host stopping, or the handler timeout) ends it early.
    /// </summary>
    /// <param name="ct">The handler cancellation token.</param>
    /// <returns>A task that completes when the pass ended.</returns>
    public async Task RunAsync(CancellationToken ct)
    {
        var batchSize = _options.Value.FulfilmentBatchSize;

        // Asked before the plugins are resolved: most passes find nothing, and resolving reads settings.
        if (!await _context.FulfilmentRecords.AnyAsync(FulfilmentRecord.DueFilter(_clock.UtcNow), ct))
        {
            return;
        }

        var plugins = (await _plugins.ResolveAllAsync(ct)).ToDictionary(p => p.PluginId, p => p.Plugin);
        if (plugins.Count == 0)
        {
            _logger.LogInformation("Fulfilment records are due but no fulfilment plugin is enabled.");
            return;
        }

        var pluginIds = plugins.Keys.ToList();
        var delivered = 0;
        var notDelivered = 0;
        for (var attempted = 0; attempted < batchSize; attempted++)
        {
            var now = _clock.UtcNow;
            var record = await NextReadyAsync(pluginIds, now, ct);
            if (record is null)
            {
                break;
            }

            if (await DeliverAsync(record, plugins[record.PluginId], now, ct))
            {
                delivered++;
            }
            else
            {
                notDelivered++;
            }
        }

        _logger.LogInformation("Fulfilment dispatch: {Delivered} delivered, {NotDelivered} not delivered.", delivered, notDelivered);
    }

    /// <summary>
    /// Reads the record to deliver next: the one longest due among those that may go now. Read again
    /// before every delivery, so a record unblocked by the one just delivered is found in the same pass.
    /// </summary>
    /// <param name="pluginIds">The ids of the enabled plugins; a record of any other is not ready.</param>
    /// <param name="now">The current moment (UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The tracked record, or <see langword="null"/> when none is ready.</returns>
    private Task<FulfilmentRecord?> NextReadyAsync(List<string> pluginIds, DateTimeOffset now, CancellationToken ct)
    {
        var unfinished = _context.FulfilmentRecords.Where(e => e.Status != FulfilmentStatus.Done);

        // The kinds are stored by name, which sorts in a different order, so the order paid, refunded,
        // lapsed (the declaration order of FulfilmentKind) is spelled out here.
        return _context.FulfilmentRecords
            .Where(FulfilmentRecord.DueFilter(now))
            .Where(r => pluginIds.Contains(r.PluginId))
            .Where(r => !unfinished.Any(e => e.InvoiceId == r.InvoiceId
                && e.PluginId == r.PluginId
                && ((e.Kind == FulfilmentKind.Paid && r.Kind != FulfilmentKind.Paid)
                    || (e.Kind == FulfilmentKind.Refunded && r.Kind == FulfilmentKind.Lapsed))))
            .OrderBy(r => r.NextAttemptAt)
            .ThenBy(r => r.Id)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Claims one record, calls its plugin and records the outcome. Nothing a plugin does is thrown from
    /// here: a failure belongs to the record, not to the pass.
    /// </summary>
    /// <param name="record">The record to deliver, as read.</param>
    /// <param name="plugin">The plugin the record names.</param>
    /// <param name="now">The moment the record was found due (UTC).</param>
    /// <param name="ct">The handler cancellation token.</param>
    /// <returns><see langword="true"/> when the plugin accepted the delivery.</returns>
    private async Task<bool> DeliverAsync(FulfilmentRecord record, IFulfilmentPlugin plugin, DateTimeOffset now, CancellationToken ct)
    {
        if (!await ClaimAsync(record, now, ct))
        {
            return false;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_options.Value.FulfilmentCallTimeout);
        try
        {
            await CallAsync(record, plugin, timeout.Token);
            record.MarkDone(_clock.UtcNow);
        }
        catch (OperationCanceledException ex) when (timeout.IsCancellationRequested && JobFailure.IsFailure(ex, ct))
        {
            // The linked token is cancelled while the handler token is not: the call outlived its own timeout.
            Fail(record, $"The plugin did not answer within {_options.Value.FulfilmentCallTimeoutSeconds} seconds.", ex);
        }
        catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
        {
            Fail(record, $"{ex.GetType().Name}: {ex.Message}", ex);
        }

        return await SaveOutcomeAsync(record, ct);
    }

    /// <summary>
    /// Takes the lease on a due record and saves it before the plugin is called. A record whose earlier
    /// claim ran out without an answer is instead recorded as failed and saved, and not delivered now.
    /// </summary>
    /// <param name="record">The due record.</param>
    /// <param name="now">The moment the record was found due (UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the caller holds the record and may call the plugin.</returns>
    private async Task<bool> ClaimAsync(FulfilmentRecord record, DateTimeOffset now, CancellationToken ct)
    {
        try
        {
            var claimed = record.Claim(now, _options.Value.FulfilmentLease);
            await _context.SaveChangesAsync(ct);
            if (!claimed)
            {
                _logger.LogWarning(
                    "Fulfilment record {RecordId} for invoice {InvoiceId} was abandoned by an earlier attempt; it was counted as failed.",
                    record.Id,
                    record.InvoiceId);
            }

            return claimed;
        }
        catch (ConcurrencyConflictException)
        {
            // Another dispatcher claimed or changed the record between the read and this save, and delivers it.
            _context.DetachAll();
            _logger.LogInformation("Fulfilment record {RecordId} was taken by another dispatcher.", record.Id);
            return false;
        }
    }

    /// <summary>Loads what the plugin is told and calls the method for what happened to the invoice.</summary>
    /// <param name="record">The claimed record.</param>
    /// <param name="plugin">The plugin to call.</param>
    /// <param name="ct">The call cancellation token, which also carries its timeout.</param>
    /// <returns>A task that completes when the plugin has answered.</returns>
    /// <exception cref="InvalidOperationException">The record kind is not one a plugin is told about.</exception>
    private async Task CallAsync(FulfilmentRecord record, IFulfilmentPlugin plugin, CancellationToken ct)
    {
        var invoice = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.Items)
            .Include(i => i.Transactions)
            .SingleAsync(i => i.Id == record.InvoiceId, ct);
        var account = await _accounts.FindAsync(invoice.ClientId, ct);

        switch (record.Kind)
        {
            case FulfilmentKind.Paid:
                await plugin.OnInvoicePaidAsync(FulfilmentContextBuilder.Paid(invoice, account), ct);
                break;
            case FulfilmentKind.Refunded:
                await plugin.OnInvoiceRefundedAsync(FulfilmentContextBuilder.Refunded(invoice, account), ct);
                break;
            case FulfilmentKind.Lapsed:
                await plugin.OnSubscriptionLapsedAsync(FulfilmentContextBuilder.Lapsed(invoice, account), ct);
                break;
            default:
                throw new InvalidOperationException($"A fulfilment record of kind {record.Kind} has no plugin method.");
        }
    }


    /// <summary>Records a failed attempt on the claimed record and logs it.</summary>
    /// <param name="record">The claimed record.</param>
    /// <param name="error">What went wrong, for staff; the record cuts it to its maximum length.</param>
    /// <param name="exception">The exception behind it, for the log.</param>
    private void Fail(FulfilmentRecord record, string error, Exception exception)
    {
        record.MarkFailed(error, _clock.UtcNow);
        _logger.LogWarning(
            exception,
            "Fulfilment record {RecordId} for invoice {InvoiceId} failed on attempt {Attempt} for plugin {PluginId}.",
            record.Id,
            record.InvoiceId,
            record.Attempts,
            record.PluginId);
    }

    /// <summary>
    /// Saves how the attempt ended. A save that fails is logged and the record stays claimed: its lease
    /// runs out and the attempt is then counted as failed, which is the same at-least-once delivery.
    /// </summary>
    /// <param name="record">The record with its outcome.</param>
    /// <param name="ct">The handler cancellation token.</param>
    /// <returns><see langword="true"/> when the plugin accepted the delivery and the outcome was saved.</returns>
    private async Task<bool> SaveOutcomeAsync(FulfilmentRecord record, CancellationToken ct)
    {
        try
        {
            await _context.SaveChangesAsync(ct);
            return record.Status == FulfilmentStatus.Done;
        }
        catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
        {
            _context.DetachAll();
            _logger.LogError(ex, "Fulfilment record {RecordId} could not save the outcome of its attempt.", record.Id);
            return false;
        }
    }
}
