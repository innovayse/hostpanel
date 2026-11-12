using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A save interceptor that makes the first save of an invoice lose on purpose: just before a save that changes the target
/// invoice, it writes the invoice row on a connection of its own, as a concurrent request would, so the save meets a newer
/// row version. It fires once and reacts only to the invoice the test owns.
/// </summary>
/// <param name="targetInvoiceId">Gives the id of the invoice to spoil; read at save time, so a test can set it after the host is built.</param>
internal sealed class InvoiceRaceSaveInterceptor(Func<int> targetInvoiceId) : SaveChangesInterceptor
{
    /// <summary>Gets whether the competing write ran.</summary>
    public bool Fired { get; private set; }

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Fired || eventData.Context is not { } db)
        {
            return result;
        }

        var id = targetInvoiceId();
        if (!db.ChangeTracker.Entries<Invoice>().Any(e => e.State == EntityState.Modified && e.Entity.Id == id))
        {
            return result;
        }

        // Committed on its own connection: the context connection holds the save transaction, which the loser rolls back.
        await using var competitor = new NpgsqlConnection(db.Database.GetConnectionString());
        await competitor.OpenAsync(cancellationToken);
        await using var update = new NpgsqlCommand(
            """UPDATE "Invoices" SET "GatewayUnreferencedNotifiedAt" = @at WHERE "Id" = @id""", competitor);
        update.Parameters.AddWithValue("at", DateTimeOffset.UtcNow);
        update.Parameters.AddWithValue("id", id);
        await update.ExecuteNonQueryAsync(cancellationToken);
        Fired = true;
        return result;
    }
}
