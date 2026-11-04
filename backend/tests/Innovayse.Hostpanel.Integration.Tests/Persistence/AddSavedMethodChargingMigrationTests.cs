using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// <c>AddSavedMethodCharging</c> on a database that already holds clients: every <c>Clients.CardGatewayCustomerId</c>
/// moves into <c>GatewayCustomers</c> as the built-in gateway's account and the column is dropped, and a customer id
/// shared by two clients stops the migration before anything changes, with the id named.
/// </summary>
public sealed class AddSavedMethodChargingMigrationTests
{
    /// <summary>The migration before the one under test.</summary>
    private const string PreviousMigration = "20261103054548_PluginDataStore";

    /// <summary>The migration under test.</summary>
    private const string MigrationUnderTest = "20261103145108_AddSavedMethodCharging";

    /// <summary>Each client's customer id becomes a <c>stripe</c> gateway customer, a client without one gets none, and the column is gone.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Up_CopiesEveryCustomerIdIntoGatewayCustomersAndDropsTheColumnAsync()
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
            await MigrateAsync(options, PreviousMigration);
            var now = DateTimeOffset.UtcNow;
            var first = await InsertClientAsync(connectionString, now, "cus_first");
            var second = await InsertClientAsync(connectionString, now, "cus_second");
            var without = await InsertClientAsync(connectionString, now, null);

            await MigrateAsync(options, MigrationUnderTest);

            (await ScalarAsync(connectionString, $"SELECT \"CustomerRef\" FROM \"GatewayCustomers\" WHERE \"ClientId\" = {first} AND \"GatewayModuleId\" = 'stripe'"))
                .Should().Be("cus_first");
            (await ScalarAsync(connectionString, $"SELECT \"CustomerRef\" FROM \"GatewayCustomers\" WHERE \"ClientId\" = {second} AND \"GatewayModuleId\" = 'stripe'"))
                .Should().Be("cus_second");
            (await ScalarAsync(connectionString, $"SELECT count(*) FROM \"GatewayCustomers\" WHERE \"ClientId\" = {without}")).Should().Be(0L);
            (await ScalarAsync(connectionString, "SELECT count(*) FROM information_schema.columns WHERE table_name = 'Clients' AND column_name = 'CardGatewayCustomerId'"))
                .Should().Be(0L, "the column moved into GatewayCustomers");

            await MigrateAsync(options, PreviousMigration);

            (await ScalarAsync(connectionString, $"SELECT \"CardGatewayCustomerId\" FROM \"Clients\" WHERE \"Id\" = {first}")).Should().Be("cus_first", "Down puts the built-in account back");
            (await ScalarAsync(connectionString, "SELECT count(*) FROM \"Clients\"")).Should().Be(3L, "Down keeps every client");
        }
        finally
        {
            using var probe = new NpgsqlConnection(connectionString);
            NpgsqlConnection.ClearPool(probe);
            await SharedPostgresServer.DropDatabaseAsync(databaseName);
        }
    }

    /// <summary>Two clients that share a customer id stop the migration, the message names the id, and nothing is changed.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Up_WithACustomerIdSharedByTwoClients_StopsAndNamesItAsync()
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
            await MigrateAsync(options, PreviousMigration);
            var now = DateTimeOffset.UtcNow;
            await InsertClientAsync(connectionString, now, "cus_shared");
            await InsertClientAsync(connectionString, now, "cus_shared");
            await InsertClientAsync(connectionString, now, "cus_alone");

            var act = () => MigrateAsync(options, MigrationUnderTest);

            var thrown = await act.Should().ThrowAsync<Exception>();
            thrown.Which.ToString().Should().Contain("'cus_shared'").And.NotContain("cus_alone");
            (await ScalarAsync(connectionString, "SELECT count(*) FROM information_schema.columns WHERE table_name = 'Clients' AND column_name = 'CardGatewayCustomerId'"))
                .Should().Be(1L, "the column is still there");
            (await ScalarAsync(connectionString, "SELECT count(*) FROM information_schema.tables WHERE table_name = 'GatewayCustomers'"))
                .Should().Be(0L, "nothing was created");
        }
        finally
        {
            using var probe = new NpgsqlConnection(connectionString);
            NpgsqlConnection.ClearPool(probe);
            await SharedPostgresServer.DropDatabaseAsync(databaseName);
        }
    }

    /// <summary>Migrates the test database to a named migration.</summary>
    /// <param name="options">The options pointing at the test database.</param>
    /// <param name="target">The migration to reach.</param>
    /// <returns>A task that completes when the database is at <paramref name="target"/>.</returns>
    private static async Task MigrateAsync(DbContextOptions<AppDbContext> options, string target)
    {
        await using var context = new AppDbContext(options, new ModelOnlyEncryptionService());
        await context.GetService<IMigrator>().MigrateAsync(target);
    }

    /// <summary>Runs a query that returns one value.</summary>
    /// <param name="connectionString">The connection string of the test database.</param>
    /// <param name="sql">The query.</param>
    /// <returns>The first column of the first row.</returns>
    private static async Task<object?> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    /// <summary>Inserts a client, naming only the columns the schema had before the migration under test.</summary>
    /// <param name="connectionString">The connection string of the test database.</param>
    /// <param name="now">The creation moment.</param>
    /// <param name="customerId">The card gateway customer id on the client, or <see langword="null"/>.</param>
    /// <returns>The client id.</returns>
    private static async Task<int> InsertClientAsync(string connectionString, DateTimeOffset now, string? customerId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO "Clients" ("UserId", "FirstName", "LastName", "Status", "CreatedAt", "CardGatewayCustomerId",
                "AllowSso", "DisableCcProcessing", "LateFees", "MarketingOptIn", "NotifyAffiliate", "NotifyDomain",
                "NotifyGeneral", "NotifyInvoice", "NotifyProduct", "NotifySupport", "OverdueNotices",
                "SeparateInvoices", "StatusUpdate", "TaxExempt")
            VALUES (@user, 'Ada', 'Lovelace', 'Active', @now, @customer,
                false, false, false, false, false, false, false, false, false, false, false, false, false, false)
            RETURNING "Id"
            """;
        command.Parameters.AddWithValue("user", $"subject-{Guid.NewGuid():N}");
        command.Parameters.AddWithValue("now", now);
        command.Parameters.AddWithValue("customer", (object?)customerId ?? DBNull.Value);
        return (int)(await command.ExecuteScalarAsync())!;
    }
}
