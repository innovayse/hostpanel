using System.Data.Common;
using Innovayse.Hostpanel.Application.Billing.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// Runs an action once, just before the card settlement's first invoice load, so a test can let
/// another writer record the payment at the exact moment between the settlement's ledger read and
/// its invoice read, inline and without any timing. A test asserts <see cref="Fired"/> and
/// <see cref="LedgerReadsBeforeFire"/>, so a run that never reached that moment cannot pass.
/// </summary>
/// <param name="action">What the other writer does; runs on the settlement's own thread of execution and is awaited.</param>
internal sealed class InvoiceLoadHookInterceptor(Func<Task> action) : DbCommandInterceptor
{
    /// <summary>Whether the action has already been taken; it runs for the first tagged command only.</summary>
    private int _taken;

    /// <summary>How many ledger reads were seen before the action was taken.</summary>
    private int _ledgerReads;

    /// <summary>Whether the action ran to completion at the settlement's invoice load.</summary>
    public bool Fired { get; private set; }

    /// <summary>How many tagged ledger reads the settlement made before the action ran; one when it read the ledger and then loaded the invoice.</summary>
    public int LedgerReadsBeforeFire => Volatile.Read(ref _ledgerReads);

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _taken) == 0
            && command.CommandText.Contains(SettlementQueryTags.LedgerRead, StringComparison.Ordinal))
        {
            Interlocked.Increment(ref _ledgerReads);
        }

        // Claimed before the action runs: the action settles through the same host, whose own invoice
        // load passes through this interceptor again and must not fire it a second time.
        if (command.CommandText.Contains(SettlementQueryTags.InvoiceLoad, StringComparison.Ordinal)
            && Interlocked.Exchange(ref _taken, 1) == 0)
        {
            await action();
            Fired = true;
        }

        return result;
    }
}
