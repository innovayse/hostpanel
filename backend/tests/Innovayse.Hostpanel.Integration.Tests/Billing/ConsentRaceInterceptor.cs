using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// Runs a step once, just before the first save that changes a card charge consent, so a test can make a competing
/// request win the race between the read and the save: the step writes the same row straight to the database and the
/// save then fails on the stale row version.
/// </summary>
/// <param name="competingWrite">The competing write: the context the save runs on and the id of the consent about to be saved.</param>
internal sealed class ConsentRaceInterceptor(Func<DbContext, int, Task> competingWrite) : SaveChangesInterceptor
{
    /// <summary>Set to 1 once the step has run, so it runs for the first save only.</summary>
    private int _fired;

    /// <summary>Gets a value indicating whether the competing write ran.</summary>
    public bool Fired => Volatile.Read(ref _fired) == 1;

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context
            && context.ChangeTracker.Entries<CardChargeConsent>().FirstOrDefault(e => e.State == EntityState.Modified) is { } entry
            && Interlocked.Exchange(ref _fired, 1) == 0)
        {
            await competingWrite(context, entry.Entity.Id);
        }

        return result;
    }
}
