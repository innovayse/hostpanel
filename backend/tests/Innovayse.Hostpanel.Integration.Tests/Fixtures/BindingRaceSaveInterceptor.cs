using Innovayse.Hostpanel.Domain.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A save interceptor that loses a binding race on purpose: just before a save that adds a
/// <see cref="PluginSlotProductBinding"/>, it commits the first added row on a connection of its own, as a concurrent
/// operator save would. The handler has already read the table by then, so only the unique index can refuse the save,
/// which is exactly the window two first saves of one widget leave open. It fires at most the given number of times,
/// so a test can lose once (the retry wins) or on every attempt (the save gives up).
/// </summary>
internal sealed class BindingRaceSaveInterceptor : SaveChangesInterceptor
{
    /// <summary>How many saves the interceptor may spoil.</summary>
    private readonly int _maxFires;

    /// <summary>Initializes the interceptor.</summary>
    /// <param name="maxFires">How many saves it may spoil; one by default.</param>
    public BindingRaceSaveInterceptor(int maxFires = 1)
    {
        _maxFires = maxFires;
    }

    /// <summary>Gets how many times the competing row was written.</summary>
    public int FireCount { get; private set; }

    /// <summary>Gets whether the competing row was written at least once.</summary>
    public bool Fired => FireCount > 0;

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (FireCount >= _maxFires || eventData.Context is not { } db)
        {
            return result;
        }

        var pending = db.ChangeTracker.Entries<PluginSlotProductBinding>().FirstOrDefault(e => e.State == EntityState.Added);
        if (pending is null)
        {
            return result;
        }

        // Committed on its own connection: the context connection holds the save transaction, which the loser rolls back.
        await using var competitor = new NpgsqlConnection(db.Database.GetConnectionString());
        await competitor.OpenAsync(cancellationToken);
        await using var insert = new NpgsqlCommand(
            """
            INSERT INTO "PluginSlotProductBindings" ("PluginId", "SlotId", "WidgetId", "ProductId", "CreatedAt")
            VALUES (@pluginId, @slotId, @widgetId, @productId, @createdAt)
            """,
            competitor);
        insert.Parameters.AddWithValue("pluginId", pending.Entity.PluginId);
        insert.Parameters.AddWithValue("slotId", pending.Entity.SlotId);
        insert.Parameters.AddWithValue("widgetId", pending.Entity.WidgetId);
        insert.Parameters.AddWithValue("productId", pending.Entity.ProductId);
        insert.Parameters.AddWithValue("createdAt", DateTimeOffset.UtcNow);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        FireCount++;
        return result;
    }
}
