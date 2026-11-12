using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// <c>StoreOrderItemDomainActionByName</c> on a database whose order lines hold the old lower-case domain actions:
/// each becomes the member name and reads back through the model, and a text that is no action stops the migration.
/// </summary>
public sealed class StoreOrderItemDomainActionByNameMigrationTests
{
    /// <summary>The migration before the one under test.</summary>
    private const string PreviousMigration = "20261108040919_AddDomainLineRenewals";

    /// <summary>The migration under test.</summary>
    private const string TargetMigration = "20261110061040_StoreOrderItemDomainActionByName";

    /// <summary>Old lower-case and padded texts become member names, read back as the action, and going down restores the lower case.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Up_OnLowerCaseActions_StoresMemberNamesAndReadsThemAsync()
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
            await MigrateToAsync(options, PreviousMigration);

            var orderId = await SeedOrderAsync(connectionString);
            var register = await SeedItemAsync(connectionString, orderId, "register");
            var transfer = await SeedItemAsync(connectionString, orderId, " Transfer ");
            var renew = await SeedItemAsync(connectionString, orderId, "RENEW");
            var service = await SeedItemAsync(connectionString, orderId, null);

            await MigrateToAsync(options, TargetMigration);

            (await ActionOfAsync(connectionString, register)).Should().Be("Register");
            (await ActionOfAsync(connectionString, transfer)).Should().Be("Transfer", "a padded text is trimmed");
            (await ActionOfAsync(connectionString, renew)).Should().Be("Renew", "the casing of the old text does not matter");
            (await ActionOfAsync(connectionString, service)).Should().BeNull("a service line has no domain action");

            await using (var context = NewContext(options))
            {
                var actions = await context.OrderItems.AsNoTracking()
                    .Where(i => i.OrderId == orderId)
                    .OrderBy(i => i.Id)
                    .Select(i => i.DomainAction)
                    .ToListAsync();
                actions.Should().Equal(DomainAction.Register, DomainAction.Transfer, DomainAction.Renew, null);
            }

            await MigrateToAsync(options, PreviousMigration);

            (await ActionOfAsync(connectionString, register)).Should().Be("register", "going down restores the text the previous release reads");
        }
        finally
        {
            await SharedPostgresServer.DropDatabaseAsync(databaseName);
        }
    }

    /// <summary>A stored text that names no domain action stops the migration and leaves the rows as they were.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Up_OnUnknownAction_FailsAndChangesNothingAsync()
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
            await MigrateToAsync(options, PreviousMigration);

            var orderId = await SeedOrderAsync(connectionString);
            var known = await SeedItemAsync(connectionString, orderId, "register");
            await SeedItemAsync(connectionString, orderId, "release");

            var act = () => MigrateToAsync(options, TargetMigration);

            (await act.Should().ThrowAsync<PostgresException>()).Which.MessageText.Should().Contain("'release'");
            (await ActionOfAsync(connectionString, known)).Should().Be("register", "a failed migration changes no row");
        }
        finally
        {
            await SharedPostgresServer.DropDatabaseAsync(databaseName);
        }
    }

    /// <summary>Migrates the database to the given migration, up or down.</summary>
    /// <param name="options">The context options naming the database.</param>
    /// <param name="migration">The migration to end on.</param>
    /// <returns>A task that completes when the database is at that migration.</returns>
    private static async Task MigrateToAsync(DbContextOptions<AppDbContext> options, string migration)
    {
        await using var context = NewContext(options);
        await context.GetService<IMigrator>().MigrateAsync(migration);
    }

    /// <summary>Opens a context over the database.</summary>
    /// <param name="options">The context options naming the database.</param>
    /// <returns>A new context.</returns>
    private static AppDbContext NewContext(DbContextOptions<AppDbContext> options) =>
        new(options, new ModelOnlyEncryptionService());

    /// <summary>Inserts a client, the product its lines order and an order of theirs, naming only columns the schema at this point has.</summary>
    /// <param name="connectionString">The connection string of the database.</param>
    /// <returns>The new order's id.</returns>
    private static async Task<int> SeedOrderAsync(string connectionString)
    {
        var clientId = await InsertAsync(
            connectionString,
            """
            INSERT INTO "Clients" ("UserId", "FirstName", "LastName", "Status", "CreatedAt", "Currency",
                "AllowSso", "DisableCcProcessing", "LateFees", "MarketingOptIn", "NotifyAffiliate", "NotifyDomain",
                "NotifyGeneral", "NotifyInvoice", "NotifyProduct", "NotifySupport", "OverdueNotices",
                "SeparateInvoices", "StatusUpdate", "TaxExempt")
            VALUES (@user, 'Ada', 'Domains', 'Active', @now, 'USD',
                false, false, false, false, false, false, false, false, false, false, false, false, false, false)
            RETURNING "Id"
            """,
            ("user", Guid.NewGuid().ToString()),
            ("now", TestMoments.Seeded));

        var groupId = await InsertAsync(
            connectionString,
            """INSERT INTO "ProductGroups" ("Name", "IsActive") VALUES (@name, true) RETURNING "Id" """,
            ("name", $"mig-{Guid.NewGuid():N}"));

        await InsertAsync(
            connectionString,
            """
            INSERT INTO "Products" ("GroupId", "Name", "Type", "Status", "MonthlyPrice", "AnnualPrice", "IsFeatured", "SortOrder", "GracePeriodDays", "CreatedAt")
            VALUES (@group, 'Domain', 'Domain', 'Active', 0, 0, false, 0, 0, @now) RETURNING "Id"
            """,
            ("group", groupId),
            ("now", DateTimeOffset.UtcNow));

        return await InsertAsync(
            connectionString,
            """
            INSERT INTO "Orders" ("OrderNumber", "ClientId", "Status", "PaymentMethod", "CreatedAt", "PaymentToken", "Currency")
            VALUES (@number, @client, 'Pending', 'Manual', @now, @token, 'USD') RETURNING "Id"
            """,
            ("number", $"DA-{Guid.NewGuid():N}"[..20]),
            ("client", clientId),
            ("now", DateTimeOffset.UtcNow),
            ("token", Guid.NewGuid().ToString("N")));
    }

    /// <summary>Inserts an order line with the given stored domain action.</summary>
    /// <param name="connectionString">The connection string of the database.</param>
    /// <param name="orderId">The order the line belongs to.</param>
    /// <param name="domainAction">The domain action exactly as stored, or <see langword="null"/> for a service line.</param>
    /// <returns>The new line's id.</returns>
    private static Task<int> SeedItemAsync(string connectionString, int orderId, string? domainAction) =>
        InsertAsync(
            connectionString,
            """
            INSERT INTO "OrderItems" ("OrderId", "ProductId", "ProductName", "BillingCycle", "Status", "FirstPaymentAmount", "RecurringAmount", "Domain", "DomainAction")
            VALUES (@order, (SELECT max("Id") FROM "Products"), 'example.com', 'Annual', 'Pending', 10, 10, 'example.com', @action) RETURNING "Id"
            """,
            ("order", orderId),
            ("action", (object?)domainAction ?? DBNull.Value));

    /// <summary>Reads the stored domain action of one order line.</summary>
    /// <param name="connectionString">The connection string of the database.</param>
    /// <param name="itemId">The id of the line.</param>
    /// <returns>The stored text, or <see langword="null"/> when the line has none.</returns>
    private static async Task<string?> ActionOfAsync(string connectionString, int itemId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"DomainAction\" FROM \"OrderItems\" WHERE \"Id\" = @id";
        command.Parameters.AddWithValue("id", itemId);
        var value = await command.ExecuteScalarAsync();
        return value is DBNull ? null : (string?)value;
    }

    /// <summary>Runs one insert that returns the new row's id.</summary>
    /// <param name="connectionString">The connection string of the database.</param>
    /// <param name="sql">The statement, ending in <c>RETURNING "Id"</c>.</param>
    /// <param name="parameters">The statement's parameters.</param>
    /// <returns>The new row's id.</returns>
    private static async Task<int> InsertAsync(string connectionString, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (int)(await command.ExecuteScalarAsync())!;
    }
}
