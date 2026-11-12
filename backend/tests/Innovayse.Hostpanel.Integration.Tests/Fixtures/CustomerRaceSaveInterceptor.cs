using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A save interceptor that loses a customer race on purpose: just before the first save that adds a
/// <see cref="GatewayCustomer"/>, it commits the same row (same client, module and reference) on a connection of its
/// own, as a concurrent first request for the same client would. The handler has read the table by then, so only the
/// unique index on client and module can refuse the save. It fires once.
/// </summary>
internal sealed class CustomerRaceSaveInterceptor : SaveChangesInterceptor
{
    /// <summary>Gets whether the competing row was written.</summary>
    public bool Fired { get; private set; }

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Fired || eventData.Context is not { } db)
        {
            return result;
        }

        var pending = db.ChangeTracker.Entries<GatewayCustomer>().FirstOrDefault(e => e.State == EntityState.Added);
        if (pending is null)
        {
            return result;
        }

        // Committed on its own connection: the context connection holds the save transaction, which the loser rolls back.
        await using var competitor = new NpgsqlConnection(db.Database.GetConnectionString());
        await competitor.OpenAsync(cancellationToken);
        await using var insert = new NpgsqlCommand(
            """
            INSERT INTO "GatewayCustomers" ("ClientId", "GatewayModuleId", "CustomerRef", "CreatedAt")
            VALUES (@clientId, @module, @customerRef, @createdAt)
            """,
            competitor);
        insert.Parameters.AddWithValue("clientId", pending.Entity.ClientId);
        insert.Parameters.AddWithValue("module", pending.Entity.GatewayModuleId);
        insert.Parameters.AddWithValue("customerRef", pending.Entity.CustomerRef);
        insert.Parameters.AddWithValue("createdAt", DateTimeOffset.UtcNow);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        Fired = true;
        return result;
    }
}
