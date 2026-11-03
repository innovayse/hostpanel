using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// Runs an action once, just before a save that writes a chosen ledger row, so a test can let another
/// writer record the same gateway payment after the row was staged and before it is written, which makes
/// the unique gateway payment index refuse the save. A test asserts <see cref="Fired"/> so a run that
/// never reached the save cannot pass.
/// </summary>
/// <param name="isTarget">Tells whether a staged ledger row is the one the other writer races.</param>
/// <param name="action">What the other writer does; awaited before the save goes on.</param>
internal sealed class LedgerRowSaveHookInterceptor(Func<Transaction, bool> isTarget, Func<Task> action) : SaveChangesInterceptor
{
    /// <summary>Whether the action has already been taken; it runs for the first matching save only.</summary>
    private int _taken;

    /// <summary>Whether the action ran to completion at a matching save.</summary>
    public bool Fired { get; private set; }

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context
            && context.ChangeTracker.Entries<Transaction>().Any(e => e.State == EntityState.Added && isTarget(e.Entity))
            && Interlocked.Exchange(ref _taken, 1) == 0)
        {
            await action();
            Fired = true;
        }

        return result;
    }
}
