using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>Tells <see cref="SettlementWindowGate"/> when the first settlement's payment is committed.</summary>
/// <param name="gate">The gate shared with the load interceptor.</param>
internal sealed class SettlementWindowCommitInterceptor(SettlementWindowGate gate) : SaveChangesInterceptor
{
    /// <inheritdoc/>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context
            && context.ChangeTracker.Entries<Transaction>().Any(e => e.State == EntityState.Added))
        {
            gate.SavingPayment(context);
        }

        return ValueTask.FromResult(result);
    }

    /// <inheritdoc/>
    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            gate.Saved(context);
        }

        return ValueTask.FromResult(result);
    }
}
