using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A save interceptor that loses a rejection race on purpose: just before a save that rejects a <see cref="ServiceChangeRequest"/>,
/// it commits the same rejection on a connection of its own, as the handler of the cancelled invoice's event would. The staff
/// handler has already read the request by then, so only the request's concurrency token can refuse its save. It fires once.
/// </summary>
internal sealed class RejectRaceSaveInterceptor : SaveChangesInterceptor
{
    /// <summary>Gets whether the competing rejection was written.</summary>
    public bool Fired { get; private set; }

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Fired || eventData.Context is not { } db)
        {
            return result;
        }

        var rejecting = db.ChangeTracker.Entries<ServiceChangeRequest>()
            .FirstOrDefault(e => e.State == EntityState.Modified && e.Entity.Status == ServiceChangeStatus.Rejected);
        if (rejecting is null)
        {
            return result;
        }

        // Committed on its own connection: the context connection holds the save transaction, which the loser rolls back.
        await using var competitor = new NpgsqlConnection(db.Database.GetConnectionString());
        await competitor.OpenAsync(cancellationToken);
        await using var update = new NpgsqlCommand(
            """
            UPDATE "ServiceChangeRequests" SET "Status" = @status, "ResolvedAt" = @resolvedAt WHERE "Id" = @id
            """,
            competitor);
        update.Parameters.AddWithValue("status", nameof(ServiceChangeStatus.Rejected));
        update.Parameters.AddWithValue("resolvedAt", DateTimeOffset.UtcNow);
        update.Parameters.AddWithValue("id", rejecting.Entity.Id);
        await update.ExecuteNonQueryAsync(cancellationToken);
        Fired = true;
        return result;
    }
}
