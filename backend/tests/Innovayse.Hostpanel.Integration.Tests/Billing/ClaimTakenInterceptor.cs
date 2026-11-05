using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// Runs a step once, just before the save that takes an invoice payment claim, so a test can let a competing run act
/// on the invoice after the handler read it and before its claim is written (hold the invoice, pay it) and see that
/// no second charge reaches the gateway. It reacts only to a claim of the invoice the test owns, so a background job or
/// another test cannot consume the shot.
/// </summary>
/// <param name="targetInvoiceId">Gives the id of the invoice whose claim fires the step; read at save time, so the test can set it after the host is built.</param>
/// <param name="step">The step to run, given the context the claim is being saved on.</param>
internal sealed class ClaimTakenInterceptor(Func<int> targetInvoiceId, Func<DbContext, Task> step) : SaveChangesInterceptor
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
            && context.ChangeTracker.Entries<InvoicePaymentClaim>().Any(e => e.State == EntityState.Added && e.Entity.InvoiceId == targetInvoiceId())
            && Interlocked.Exchange(ref _fired, 1) == 0)
        {
            await step(context);
        }

        return result;
    }
}
