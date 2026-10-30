using System.Data.Common;
using Innovayse.Hostpanel.Application.Billing.Commands.DispatchFulfilmentNow;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Infrastructure.Persistence;

/// <summary>
/// Writes the <see cref="FulfilmentRecord"/>s a digital invoice's payment, refund or lapse owes the enabled
/// fulfilment plugins in the very save that records it, then kicks the dispatch once that save has committed.
/// </summary>
/// <remarks>
/// <para>
/// Every path that pays, refunds or lapses an invoice (gateway completion, staff mark paid, credit, an added
/// payment, a refund, the overdue sweep) ends in a save of the invoice, so watching the invoice in the change
/// tracker covers them all in one place and none can forget the records. The records are added to the same
/// unit of work, so they commit with the payment or not at all: a crash can never leave a paid invoice
/// without them. This is why the work happens before the save and not after, unlike the domain event delivery.
/// </para>
/// <para>
/// One record per enabled plugin and kind is written for an invoice at most once: a record that already
/// exists (saved earlier, or added in this same unit of work) is skipped, so a payment reported twice, or a
/// second part refund, finds its record instead of making a second delivery. The unique index on invoice,
/// plugin and kind is the last guard. With no fulfilment plugin enabled nothing is written.
/// </para>
/// <para>
/// A save in a scope that discards domain events (<see cref="IDomainEventGate"/>: the demo reset, a bulk
/// import) writes no records, exactly as it announces nothing. An invoice inserted already settled is not
/// watched: it has no id before the insert and is a bulk load, not a payment. The kick after the commit is
/// best effort and a different command from the scheduled chain's, so it can never start a second chain; if
/// it is lost, the scheduled chain delivers the records. This interceptor must be registered before
/// <see cref="AuditableEntityInterceptor"/>, so the records it adds are stamped on insert.
/// </para>
/// </remarks>
public sealed class FulfilmentRecordInterceptor : SaveChangesInterceptor, IDbTransactionInterceptor
{
    /// <summary>The scope's provider; the plugin resolver and the bus are resolved lazily so building the context never builds them.</summary>
    private readonly IServiceProvider _services;

    /// <summary>Tells whether this scope asked for its domain events, and so these records, to be discarded.</summary>
    private readonly IDomainEventGate _gate;

    /// <summary>The clock the records are due from.</summary>
    private readonly IClock _clock;

    /// <summary>Records a kick that failed after the data was already committed.</summary>
    private readonly ILogger<FulfilmentRecordInterceptor> _logger;

    /// <summary>Whether records were written by a save and the dispatch has not been kicked for them yet.</summary>
    private bool _kickPending;

    /// <summary>Initializes a new instance of the <see cref="FulfilmentRecordInterceptor"/> class.</summary>
    /// <param name="services">The scope's provider; the plugin resolver and the bus are resolved lazily.</param>
    /// <param name="gate">Tells whether this scope asked for its domain events to be discarded.</param>
    /// <param name="clock">The clock the records are due from.</param>
    /// <param name="logger">Records a kick that failed after the data was already committed.</param>
    public FulfilmentRecordInterceptor(
        IServiceProvider services, IDomainEventGate gate, IClock clock, ILogger<FulfilmentRecordInterceptor> logger)
    {
        _services = services;
        _gate = gate;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null || _gate.IsSuppressed)
        {
            return result;
        }

        var owed = FindOwed(eventData.Context);
        if (owed.Count == 0)
        {
            return result;
        }

        var plugins = await _services.GetRequiredService<IFulfilmentPluginResolver>().ResolveAllAsync(cancellationToken);
        if (plugins.Count == 0)
        {
            return result;
        }

        var existing = await ExistingKeysAsync(eventData.Context, owed.Select(o => o.InvoiceId).Distinct().ToList(), cancellationToken);
        var now = _clock.UtcNow;
        foreach (var (invoiceId, kind) in owed)
        {
            foreach (var plugin in plugins)
            {
                if (existing.Add((invoiceId, plugin.PluginId, kind)))
                {
                    eventData.Context.Set<FulfilmentRecord>().Add(FulfilmentRecord.Create(invoiceId, plugin.PluginId, kind, now));
                    _kickPending = true;
                }
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        // Inside an explicit transaction nothing has committed yet: its commit kicks, its rollback forgets.
        if (_kickPending && eventData.Context?.Database.CurrentTransaction is null)
        {
            await KickAsync();
        }

        return result;
    }

    /// <inheritdoc/>
    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        // The records were never committed (and stay tracked, so a retried save writes them again).
        if (eventData.Context?.Database.CurrentTransaction is null)
        {
            _kickPending = false;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        // A transaction disposed without commit or rollback reports neither; whatever it held never committed.
        _kickPending = false;
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc/>
    public async Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (_kickPending)
        {
            await KickAsync();
        }
    }

    /// <inheritdoc/>
    public Task TransactionRolledBackAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _kickPending = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
    {
        _kickPending = false;
    }

    /// <inheritdoc/>
    public Task TransactionFailedAsync(
        DbTransaction transaction,
        TransactionErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _kickPending = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Finds what this save does to digital invoices that plugins must hear about: an invoice that turned Paid,
    /// that turned Refunded or PartiallyRefunded, or that gained a lapse time. Invoices without a digital line
    /// owe nothing.
    /// </summary>
    /// <param name="context">The context that is about to save.</param>
    /// <returns>The invoice id and kind of every record owed, in change tracker order.</returns>
    private static List<(int InvoiceId, FulfilmentKind Kind)> FindOwed(DbContext context)
    {
        var owed = new List<(int InvoiceId, FulfilmentKind Kind)>();
        foreach (var entry in context.ChangeTracker.Entries<Invoice>())
        {
            if (entry.State != EntityState.Modified || !entry.Entity.HasDigitalItems)
            {
                continue;
            }

            if (TurnedTo(entry, InvoiceStatus.Paid))
            {
                owed.Add((entry.Entity.Id, FulfilmentKind.Paid));
            }

            if (TurnedTo(entry, InvoiceStatus.Refunded) || TurnedTo(entry, InvoiceStatus.PartiallyRefunded))
            {
                owed.Add((entry.Entity.Id, FulfilmentKind.Refunded));
            }

            var lapsedAt = entry.Property(i => i.LapsedAt);
            if (lapsedAt.IsModified && lapsedAt.OriginalValue is null && lapsedAt.CurrentValue is not null)
            {
                owed.Add((entry.Entity.Id, FulfilmentKind.Lapsed));
            }
        }

        return owed;
    }

    /// <summary>Tells whether the save moves an invoice into a status it was not in.</summary>
    /// <param name="entry">The tracked invoice.</param>
    /// <param name="status">The status to test for.</param>
    /// <returns><see langword="true"/> when the invoice is in <paramref name="status"/> now and was not when it was loaded.</returns>
    private static bool TurnedTo(EntityEntry<Invoice> entry, InvoiceStatus status)
    {
        var property = entry.Property(i => i.Status);
        return property.IsModified && property.CurrentValue == status && property.OriginalValue != status;
    }

    /// <summary>
    /// Reads which records already exist for the invoices, saved or added in this unit of work, so none is
    /// written twice.
    /// </summary>
    /// <param name="context">The context that is about to save.</param>
    /// <param name="invoiceIds">The invoices to look up.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The invoice, plugin and kind of every record that exists; the caller adds the ones it writes.</returns>
    private static async Task<HashSet<(int InvoiceId, string PluginId, FulfilmentKind Kind)>> ExistingKeysAsync(
        DbContext context, List<int> invoiceIds, CancellationToken ct)
    {
        var saved = await context.Set<FulfilmentRecord>()
            .AsNoTracking()
            .Where(r => invoiceIds.Contains(r.InvoiceId))
            .Select(r => new { r.InvoiceId, r.PluginId, r.Kind })
            .ToListAsync(ct);

        var keys = saved.Select(r => (r.InvoiceId, r.PluginId, r.Kind)).ToHashSet();
        foreach (var added in context.ChangeTracker.Entries<FulfilmentRecord>().Where(e => e.State == EntityState.Added))
        {
            keys.Add((added.Entity.InvoiceId, added.Entity.PluginId, added.Entity.Kind));
        }

        return keys;
    }

    /// <summary>Starts one extra dispatch pass for the records that just committed; best effort.</summary>
    /// <returns>A task that completes once the command has been handed to the bus or the failure logged.</returns>
    private async Task KickAsync()
    {
        _kickPending = false;
        try
        {
            await _services.GetRequiredService<IMessageBus>().PublishAsync(new DispatchFulfilmentNowCommand());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The records are saved; the scheduled chain delivers them, so a lost kick only costs time.
            _logger.LogWarning(ex, "Kicking the fulfilment dispatch failed after the save committed");
        }
    }
}
