using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// Runs an action once, just before the card settlement's first invoice load, so a test can let
/// another writer record the payment at the exact moment between the settlement's ledger read and
/// its invoice read, inline and without any timing.
/// </summary>
/// <param name="action">What the other writer does; runs on the settlement's own thread of execution and is awaited.</param>
internal sealed class InvoiceLoadHookInterceptor(Func<Task> action) : DbCommandInterceptor
{
    /// <summary>Whether the action has already been taken; it runs for the first tagged command only.</summary>
    private int _taken;

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        // Claimed before the action runs: the action settles through the same host, whose own invoice
        // load passes through this interceptor again and must not fire it a second time.
        if (command.CommandText.Contains(SettlementWindowGate.InvoiceLoadTag, StringComparison.Ordinal)
            && Interlocked.Exchange(ref _taken, 1) == 0)
        {
            await action();
        }

        return result;
    }
}
