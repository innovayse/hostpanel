using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// <c>StoredCredentialsImportIdsAndRefundClaims</c> on a database that holds clients with a legacy card customer id:
/// each non-empty id is copied into <c>GatewayCustomers</c> before the column is dropped, and Down copies it back.
/// </summary>
public sealed class StoredCredentialsImportIdsAndRefundClaimsMigrationTests
{
    /// <summary>The migration before the one under test.</summary>
    private const string PreviousMigration = "20261104055416_ImportDedupeIndexes";

    /// <summary>The migration under test.</summary>
    private const string MigrationUnderTest = "20261104083813_StoredCredentialsImportIdsAndRefundClaims";

    /// <summary>Only clients with a non-empty legacy id get a gateway customer; Down restores the column from it.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UpAndDown_CopyLegacyCardCustomersAsync()
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
            await MigrateAsync(options, PreviousMigration);
            var withCustomer = await InsertClientAsync(connectionString, "cus_legacy_1");
            var withNull = await InsertClientAsync(connectionString, null);
            var withEmpty = await InsertClientAsync(connectionString, string.Empty);

            await MigrateAsync(options, MigrationUnderTest);

            (await ScalarAsync(connectionString, "SELECT count(*) FROM \"GatewayCustomers\"")).Should().Be(1L);
            (await ScalarAsync(connectionString, $"SELECT \"CustomerRef\" FROM \"GatewayCustomers\" WHERE \"ClientId\" = {withCustomer} AND \"GatewayModuleId\" = 'stripe'"))
                .Should().Be("cus_legacy_1");
            (await ScalarAsync(connectionString, "SELECT count(*) FROM information_schema.columns WHERE table_name = 'Clients' AND column_name = 'CardGatewayCustomerId'"))
                .Should().Be(0L, "the legacy column is dropped once its values are copied");
            (await ScalarAsync(connectionString, "SELECT count(*) FROM information_schema.columns WHERE table_name = 'CardChargeConsents' AND column_name IN ('ConsentTextVersion', 'IpAddress', 'UserAgent', 'IsDefault')"))
                .Should().Be(4L, "a stored card consent records the consent text version, the caller and whether it is the default");

            await MigrateAsync(options, PreviousMigration);

            (await ScalarAsync(connectionString, $"SELECT \"CardGatewayCustomerId\" FROM \"Clients\" WHERE \"Id\" = {withCustomer}")).Should().Be("cus_legacy_1");
            (await ScalarAsync(connectionString, $"SELECT \"CardGatewayCustomerId\" FROM \"Clients\" WHERE \"Id\" = {withNull}")).Should().BeOfType<DBNull>();
            (await ScalarAsync(connectionString, $"SELECT \"CardGatewayCustomerId\" FROM \"Clients\" WHERE \"Id\" = {withEmpty}")).Should().BeOfType<DBNull>();
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

    /// <summary>Inserts a client as the schema before the migration holds it, with a legacy card customer id.</summary>
    /// <param name="connectionString">The connection string of the test database.</param>
    /// <param name="cardCustomerId">The legacy card customer id, or <see langword="null"/>.</param>
    /// <returns>The client id.</returns>
    private static async Task<int> InsertClientAsync(string connectionString, string? cardCustomerId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "Clients" ("UserId", "FirstName", "LastName", "Status", "CreatedAt", "CardGatewayCustomerId",
                "AllowSso", "DisableCcProcessing", "LateFees", "MarketingOptIn", "NotifyAffiliate", "NotifyDomain",
                "NotifyGeneral", "NotifyInvoice", "NotifyProduct", "NotifySupport", "OverdueNotices",
                "SeparateInvoices", "StatusUpdate", "TaxExempt")
            VALUES (@user, 'Ada', 'Lovelace', 'Active', @now, @card,
                false, false, false, false, false, false, false, false, false, false, false, false, false, false)
            RETURNING "Id"
            """;
        command.Parameters.AddWithValue("user", $"subject-{Guid.NewGuid():N}");
        command.Parameters.AddWithValue("now", DateTimeOffset.UtcNow);
        command.Parameters.AddWithValue("card", (object?)cardCustomerId ?? DBNull.Value);
        return (int)(await command.ExecuteScalarAsync())!;
    }
}
