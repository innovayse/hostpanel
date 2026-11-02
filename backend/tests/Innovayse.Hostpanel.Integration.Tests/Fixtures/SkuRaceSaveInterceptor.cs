using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A save interceptor that loses a SKU race on purpose: just before the first save that writes a
/// product holding <paramref name="sku"/>, it commits another product with the same SKU. The handler's
/// own uniqueness check has already passed by then, so only the unique index <c>IX_Products_Sku</c>
/// can refuse the save, which is exactly the window a concurrent writer leaves open.
/// </summary>
/// <param name="sku">The normalised SKU the competing product takes.</param>
internal sealed class SkuRaceSaveInterceptor(string sku) : SaveChangesInterceptor
{
    /// <summary>The name the competing product is stored under, so a test can tell it from the loser.</summary>
    public const string CompetitorName = "Competitor";

    /// <summary>Gets whether the competing product was written.</summary>
    public bool Fired { get; private set; }

    /// <inheritdoc/>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Fired || eventData.Context is not { } db)
        {
            return result;
        }

        var pending = db.ChangeTracker.Entries<Product>()
            .FirstOrDefault(e => e.State is EntityState.Added or EntityState.Modified && e.Entity.Sku == sku);
        if (pending is null)
        {
            return result;
        }

        // Written on a connection of its own and committed there, the way another request's product would
        // be. The context's own connection cannot be used: the domain event interceptor has already opened
        // the save's transaction by now, so a write on it would be rolled back together with the loser.
        await using var competitor = new NpgsqlConnection(db.Database.GetConnectionString());
        await competitor.OpenAsync(cancellationToken);
        await using var insert = new NpgsqlCommand(
            """
            INSERT INTO "Products" ("GroupId", "Name", "Type", "Status", "MonthlyPrice", "AnnualPrice", "IsFeatured", "SortOrder", "CreatedAt", "Sku")
            VALUES (@groupId, @name, @type, @status, 0, 0, FALSE, 0, @createdAt, @sku)
            """,
            competitor);
        insert.Parameters.AddWithValue("groupId", pending.Entity.GroupId);
        insert.Parameters.AddWithValue("name", CompetitorName);
        insert.Parameters.AddWithValue("type", ProductType.Digital.ToString());
        insert.Parameters.AddWithValue("status", ProductStatus.Active.ToString());
        insert.Parameters.AddWithValue("createdAt", DateTimeOffset.UtcNow);
        insert.Parameters.AddWithValue("sku", sku);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        Fired = true;
        return result;
    }
}
