using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// Runs a step once, just before the save that takes an invoice payment claim, so a test can change what the
/// handler read before it took the claim (withdraw the consent, pay the invoice) and see that it reads it again
/// before any money moves.
/// </summary>
/// <param name="step">The step to run, given the context the claim is being saved on.</param>
internal sealed class ClaimTakenInterceptor(Func<DbContext, Task> step) : SaveChangesInterceptor
{
    /// <summary>Set to 1 once the step has run, so it runs for the first claim only.</summary>
    private int _fired;

    /// <summary>Gets a value indicating whether the step ran.</summary>
    public bool Fired => Volatile.Read(ref _fired) == 1;

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context
            && context.ChangeTracker.Entries<InvoicePaymentClaim>().Any(e => e.State == EntityState.Added)
            && Interlocked.Exchange(ref _fired, 1) == 0)
        {
            await step(context);
        }

        return result;
    }
}
