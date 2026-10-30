using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>Stops the settlement's invoice load at <see cref="SettlementWindowGate"/> before the query runs.</summary>
/// <param name="gate">The gate shared with the commit interceptor.</param>
internal sealed class SettlementWindowLoadInterceptor(SettlementWindowGate gate) : DbCommandInterceptor
{
    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains(SettlementWindowGate.InvoiceLoadTag, StringComparison.Ordinal))
        {
            await gate.AtInvoiceLoadAsync(eventData.Context, cancellationToken);
        }

        return result;
    }
}
