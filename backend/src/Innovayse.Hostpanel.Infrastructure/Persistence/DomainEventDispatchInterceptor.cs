using System.Data.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.SharedKernel.Domain;
using Innovayse.Hostpanel.SharedKernel.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.EntityFrameworkCore;
using Wolverine.Runtime;

namespace Innovayse.Hostpanel.Infrastructure.Persistence;

/*
 * Design: a domain event is written to Wolverine's PostgreSQL outbox in the same database transaction
 * as the state change that raised it, so an event exists if and only if its change committed.
 *
 * Before #802 the events were published to in-memory queues after the commit: a crash between the
 * commit and the publish lost them, and a publish failure was logged and forgotten. Now the envelope
 * rows are inserted by Wolverine's EfCoreEnvelopeTransaction with a command on the context's current
 * transaction, so a rollback removes them with the data, and a crash after the commit leaves them in
 * the inbox tables for the durability agent to recover.
 *
 * The interceptor runs in one of two modes per save:
 *
 *   Owned      the save had no transaction. SavingChangesAsync opens one (only when the gate is open
 *              and a tracked entity has events), SavedChangesAsync writes the envelopes, commits, and
 *              only then flushes them to the local queues. A failed or cancelled save rolls it back.
 *   Caller's   the save ran inside a transaction the caller opened. The envelopes are written into it
 *              and the outbox is held until that transaction commits (then flushed) or rolls back,
 *              fails, or is abandoned for a new one (then dropped; its rows went with the rollback).
 *
 * Each batch gets its own DbContextOutbox instead of the scope's registered IDbContextOutbox: the
 * registered one flushes only once per scope (MultiFlushMode.OnlyOnce), so a scope's second save would
 * never hand its envelopes to the queues, and it has no way to drop pending envelopes except
 * ClearAllAsync, which rolls back whatever transaction is current on the context, including a new one.
 * A batch outbox is dropped by forgetting it.
 */

/// <summary>
/// Writes the domain events entities raised into Wolverine's durable outbox, inside the transaction
/// that saves the change, and hands them to the local queues once that transaction has committed.
/// </summary>
/// <remarks>
/// <para>
/// Events are read after the save, not before, because a creation event names the entity's own id and
/// the database assigns it on insert; before the save only <see cref="BaseEntity.HasDomainEvents"/> is
/// asked, which builds nothing. A save that throws leaves the events on the entities, so a retried
/// save still carries them. A publish that throws fails the save and rolls the owned transaction back:
/// the write and its announcement are one unit.
/// </para>
/// <para>
/// The synchronous <c>SaveChanges</c> and <c>Commit</c> are deliberately not intercepted: writing the
/// outbox is asynchronous and nothing in the product saves or commits synchronously. A synchronous
/// save leaves its events on the entities for the next asynchronous save of the same context.
/// </para>
/// </remarks>
public sealed class DomainEventDispatchInterceptor : SaveChangesInterceptor, IDbTransactionInterceptor
{
    /// <summary>
    /// The scope's provider; the Wolverine runtime is resolved lazily so building the context never builds it.
    /// </summary>
    private readonly IServiceProvider _services;

    /// <summary>Tells whether this scope asked for its events to be discarded.</summary>
    private readonly IDomainEventGate _gate;

    /// <summary>The transaction this interceptor opened for the current save; null outside owned mode.</summary>
    private IDbContextTransaction? _ownedTransaction;

    /// <summary>
    /// Envelopes written inside the caller's open transaction, waiting for its commit; null when nothing waits.
    /// </summary>
    private DbContextOutbox? _awaitingCommit;

    /// <summary>Initializes a new instance of the <see cref="DomainEventDispatchInterceptor"/> class.</summary>
    /// <param name="services">The scope's provider; the Wolverine runtime is resolved lazily.</param>
    /// <param name="gate">Tells whether this scope asked for its events to be discarded.</param>
    public DomainEventDispatchInterceptor(IServiceProvider services, IDomainEventGate gate)
    {
        _services = services;
        _gate = gate;
    }

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context is null || _gate.IsSuppressed || context.Database.CurrentTransaction is not null)
        {
            return result;
        }

        if (!context.ChangeTracker.Entries<BaseEntity>().Any(entry => entry.Entity.HasDomainEvents))
        {
            // No events, no transaction: a plain save keeps EF's own implicit transaction, as before.
            return result;
        }

        // Opened before the data is written, so the data and the envelope rows share one commit.
        _ownedTransaction = await context.Database.BeginTransactionAsync(cancellationToken);
        return result;
    }

    /// <inheritdoc/>
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context is null)
        {
            return result;
        }

        var raised = TakeEvents(context);
        if (_gate.IsSuppressed || (raised.Count == 0 && _ownedTransaction is null))
        {
            // Suppressed: taken and discarded, so a bulk writer's events are never announced later.
            return result;
        }

        if (_ownedTransaction is null && context.Database.CurrentTransaction is not null)
        {
            await WriteIntoCallerTransactionAsync(context, raised);
            return result;
        }

        await WriteAndCommitOwnedAsync(context, raised, cancellationToken);
        return result;
    }

    /// <inheritdoc/>
    public override async Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await RollbackOwnedAsync();
    }

    /// <inheritdoc/>
    public override async Task SaveChangesCanceledAsync(
        DbContextEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await RollbackOwnedAsync();
    }

    /// <inheritdoc/>
    public ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection,
        TransactionEndEventData eventData,
        DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        // A transaction disposed without commit or rollback reports neither; whatever it held never
        // committed, so a new transaction starts with nothing waiting.
        _awaitingCommit = null;
        return ValueTask.FromResult(result);
    }

    /// <inheritdoc/>
    public async Task TransactionCommittedAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        // The owned commit in WriteAndCommitOwnedAsync also lands here; it never sets _awaitingCommit,
        // so it is a no-op and the owned batch is flushed once, by its own method.
        var committed = _awaitingCommit;
        if (committed is null)
        {
            return;
        }

        // Taken first, so a handler that saves on this context cannot flush the batch twice.
        _awaitingCommit = null;
        await committed.FlushOutgoingMessagesAsync();
    }

    /// <inheritdoc/>
    public Task TransactionRolledBackAsync(
        DbTransaction transaction,
        TransactionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        // The envelope rows rolled back with the data; the in-memory copies must not be sent.
        _awaitingCommit = null;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
    {
        _awaitingCommit = null;
    }

    /// <inheritdoc/>
    public Task TransactionFailedAsync(
        DbTransaction transaction,
        TransactionErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        // A failed commit or rollback leaves nothing known to be committed.
        _awaitingCommit = null;
        return Task.CompletedTask;
    }

    /// <summary>Takes every tracked aggregate's events and clears them, in raise order.</summary>
    /// <param name="context">The context that just saved.</param>
    /// <returns>The events taken; empty when nothing was raised.</returns>
    private static List<IDomainEvent> TakeEvents(DbContext context)
    {
        var pending = new List<IDomainEvent>();
        foreach (var entity in context.ChangeTracker.Entries<BaseEntity>().Select(e => e.Entity))
        {
            if (!entity.HasDomainEvents)
            {
                continue;
            }

            // Built here, after the insert, so a deferred creation event carries the assigned id.
            pending.AddRange(entity.DomainEvents);

            // Cleared before writing, so an event can never be written twice, however the save ends.
            entity.ClearDomainEvents();
        }

        return pending;
    }

    /// <summary>
    /// Owned mode: writes the envelopes into the transaction this interceptor opened, commits it, then
    /// hands the envelopes to the local queues.
    /// </summary>
    /// <param name="context">The context that just saved.</param>
    /// <param name="raised">The events taken from the saved entities.</param>
    /// <param name="ct">Cancellation token of the save.</param>
    /// <returns>A task that completes once the batch is committed and flushed.</returns>
    private async Task WriteAndCommitOwnedAsync(
        DbContext context,
        IReadOnlyList<IDomainEvent> raised,
        CancellationToken ct)
    {
        // An entity can raise an event during the save itself, after SavingChangesAsync found none; its
        // data already committed in EF's implicit transaction, so its envelopes get a transaction of their own.
        _ownedTransaction ??= await context.Database.BeginTransactionAsync(ct);

        var outbox = NewOutbox(context);
        try
        {
            await PublishAsync(outbox, raised);
        }
        catch
        {
            // Rolled back here and the field cleared, so SaveChangesFailedAsync, which EF calls next for
            // the same exception, finds nothing left to roll back.
            await RollbackOwnedAsync();
            throw;
        }

        try
        {
            await _ownedTransaction.CommitAsync(ct);
        }
        catch
        {
            // A commit that threw has ended the transaction either way; rolling it back again would
            // throw over the real error, so it is only disposed. Its envelopes are never flushed.
            await DisposeOwnedAsync();
            throw;
        }

        await DisposeOwnedAsync();

        // After the commit, never before: a handler must not run for a change that could still roll back.
        // Should the process die between the commit and this line, the durability agent recovers the
        // committed envelopes from the inbox tables.
        await outbox.FlushOutgoingMessagesAsync();
    }

    /// <summary>
    /// Caller's mode: writes the envelopes into the caller's open transaction and keeps the outbox until
    /// that transaction commits, rolls back, fails or is abandoned.
    /// </summary>
    /// <param name="context">The context that just saved.</param>
    /// <param name="raised">The events taken from the saved entities.</param>
    /// <returns>A task that completes once every envelope row is written.</returns>
    private async Task WriteIntoCallerTransactionAsync(DbContext context, IReadOnlyList<IDomainEvent> raised)
    {
        if (raised.Count == 0)
        {
            return;
        }

        // A second save inside the same transaction adds to the batch already waiting, so one commit
        // flushes everything that transaction wrote.
        var outbox = _awaitingCommit ?? NewOutbox(context);
        await PublishAsync(outbox, raised);
        _awaitingCommit = outbox;
    }

    /// <summary>Builds a fresh outbox enrolled on <paramref name="context"/>'s current transaction.</summary>
    /// <param name="context">The context whose transaction the envelope rows are written in.</param>
    /// <returns>An outbox that writes its envelopes with commands on the context's current transaction.</returns>
    private DbContextOutbox NewOutbox(DbContext context)
    {
        // No scrapers: this interceptor is the only place domain events are collected.
        var outbox = new DbContextOutbox(_services.GetRequiredService<IWolverineRuntime>(), []);
        outbox.Enroll(context);
        return outbox;
    }

    /// <summary>Publishes every event through <paramref name="outbox"/>, in raise order.</summary>
    /// <param name="outbox">The batch outbox that writes the envelope rows.</param>
    /// <param name="raised">The events to publish.</param>
    /// <returns>A task that completes once every envelope row is written.</returns>
    private static async Task PublishAsync(DbContextOutbox outbox, IReadOnlyList<IDomainEvent> raised)
    {
        foreach (var domainEvent in raised)
        {
            // Published as object so the runtime type picks the route: each handler subscribed to the
            // concrete event runs, not those of IDomainEvent. A failure propagates and fails the save.
            await outbox.PublishAsync((object)domainEvent);
        }
    }

    /// <summary>Rolls back and forgets the owned transaction, if there is one.</summary>
    /// <returns>A task that completes once the transaction is rolled back and disposed.</returns>
    private async Task RollbackOwnedAsync()
    {
        if (_ownedTransaction is null)
        {
            return;
        }

        try
        {
            // Not the save's token: a cancelled save must still release its transaction.
            await _ownedTransaction.RollbackAsync(CancellationToken.None);
        }
        finally
        {
            await DisposeOwnedAsync();
        }
    }

    /// <summary>Disposes and forgets the owned transaction, so the next save starts in a clean state.</summary>
    /// <returns>A task that completes once the transaction is disposed.</returns>
    private async Task DisposeOwnedAsync()
    {
        var owned = _ownedTransaction;
        _ownedTransaction = null;
        if (owned is not null)
        {
            await owned.DisposeAsync();
        }
    }
}
