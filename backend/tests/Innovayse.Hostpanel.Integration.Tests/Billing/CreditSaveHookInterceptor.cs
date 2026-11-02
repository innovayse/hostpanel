using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// Runs an action once, just before a save that writes a ledger row credited to a client's balance,
/// so a test can let another writer move the balance after the credit was staged and before it is
/// written, which makes that save lose. A test asserts <see cref="Fired"/> so a run that never
/// reached the save cannot pass.
/// </summary>
/// <param name="action">What the other writer does; awaited before the save goes on.</param>
internal sealed class CreditSaveHookInterceptor(Func<Task> action) : SaveChangesInterceptor
{
    /// <summary>Whether the action has already been taken; it runs for the first credit save only.</summary>
    private int _taken;

    /// <summary>Whether the action ran to completion at a credit save.</summary>
    public bool Fired { get; private set; }

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context
            && context.ChangeTracker.Entries<Transaction>().Any(e => e.State == EntityState.Added && e.Entity.AddedToCredit)
            && Interlocked.Exchange(ref _taken, 1) == 0)
        {
            await action();
            Fired = true;
        }

        return result;
    }
}
