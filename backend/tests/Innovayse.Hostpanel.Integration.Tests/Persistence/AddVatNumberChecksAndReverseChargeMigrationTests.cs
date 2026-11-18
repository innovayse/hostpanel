using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// <c>AddVatNumberChecksAndReverseCharge</c> on a database that already holds clients and invoices: every row survives with
/// its data, no client has a verdict and no invoice is under the reverse charge, the new columns carry PascalCase names, and
/// going back down keeps every row.
/// </summary>
public sealed class AddVatNumberChecksAndReverseChargeMigrationTests
{
    /// <summary>The migration before the one under test.</summary>
    private const string PreviousMigration = "20261116094807_AddVatRatesAndDigitalServiceFlag";

    /// <summary>The migration under test.</summary>
    private const string MigrationUnderTest = "20261116102809_AddVatNumberChecksAndReverseCharge";

    /// <summary>A client and an invoice that exist before the migration keep their data and gain the empty state of the new columns.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Up_KeepsEveryExistingRowAndGivesItTheEmptyStateAsync()
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
            await MigrateAsync(options, PreviousMigration);
            var now = TestMoments.Seeded;
            var clientId = await InsertClientAsync(connectionString, now, "DE123456789");
            var invoiceId = await InsertInvoiceAsync(connectionString, clientId, now);

            await MigrateAsync(options, MigrationUnderTest);

            (await ScalarAsync(connectionString, $"SELECT \"TaxId\" FROM \"Clients\" WHERE \"Id\" = {clientId}")).Should().Be("DE123456789");
            (await ScalarAsync(connectionString, $"SELECT \"VatStatus\" FROM \"Clients\" WHERE \"Id\" = {clientId}")).Should().BeOfType<DBNull>();
            (await ScalarAsync(connectionString, $"SELECT \"VatCheckedAt\" FROM \"Clients\" WHERE \"Id\" = {clientId}")).Should().BeOfType<DBNull>();
            (await ScalarAsync(connectionString, $"SELECT \"VatName\" FROM \"Clients\" WHERE \"Id\" = {clientId}")).Should().BeOfType<DBNull>();
            (await ScalarAsync(connectionString, $"SELECT \"VatAddress\" FROM \"Clients\" WHERE \"Id\" = {clientId}")).Should().BeOfType<DBNull>();
            (await ScalarAsync(connectionString, $"SELECT \"ReverseCharge\" FROM \"Invoices\" WHERE \"Id\" = {invoiceId}")).Should().Be(false);
            (await ScalarAsync(connectionString, $"SELECT \"ClientVatNumber\" FROM \"Invoices\" WHERE \"Id\" = {invoiceId}")).Should().BeOfType<DBNull>();
            (await ScalarAsync(
                connectionString,
                "SELECT count(*) FROM information_schema.columns WHERE (table_name = 'Clients' AND column_name IN ('VatStatus', 'VatCheckedAt', 'VatName', 'VatAddress')) "
                + "OR (table_name = 'Invoices' AND column_name IN ('ReverseCharge', 'ClientVatNumber'))"))
                .Should().Be(6L, "the new columns are PascalCase, never snake or lower case");

            await MigrateAsync(options, PreviousMigration);

            (await ScalarAsync(connectionString, "SELECT count(*) FROM \"Clients\"")).Should().Be(1L, "Down keeps every client");
            (await ScalarAsync(connectionString, "SELECT count(*) FROM \"Invoices\"")).Should().Be(1L, "Down keeps every invoice");
            (await ScalarAsync(connectionString, $"SELECT \"TaxId\" FROM \"Clients\" WHERE \"Id\" = {clientId}")).Should().Be("DE123456789");
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
    /// <param name="taxId">The client's tax identification number.</param>
    /// <returns>The client id.</returns>
    private static async Task<int> InsertClientAsync(string connectionString, DateTimeOffset now, string taxId)
    {
        return await InsertAsync(
            connectionString,
            """
            INSERT INTO "Clients" ("UserId", "FirstName", "LastName", "Status", "CreatedAt", "TaxId",
                "AllowSso", "DisableCcProcessing", "LateFees", "MarketingOptIn", "NotifyAffiliate", "NotifyDomain",
                "NotifyGeneral", "NotifyInvoice", "NotifyProduct", "NotifySupport", "OverdueNotices",
                "SeparateInvoices", "StatusUpdate", "TaxExempt")
            VALUES (@user, 'Ada', 'Lovelace', 'Active', @now, @taxId,
                false, false, false, false, false, false, false, false, false, false, false, false, false, false)
            RETURNING "Id"
            """,
            ("user", $"subject-{Guid.NewGuid():N}"),
            ("now", now),
            ("taxId", taxId));
    }

    /// <summary>Inserts an unpaid invoice.</summary>
    /// <param name="connectionString">The connection string of the test database.</param>
    /// <param name="clientId">The owning client.</param>
    /// <param name="now">The creation moment.</param>
    /// <returns>The invoice id.</returns>
    private static async Task<int> InsertInvoiceAsync(string connectionString, int clientId, DateTimeOffset now)
    {
        return await InsertAsync(
            connectionString,
            """
            INSERT INTO "Invoices" ("ClientId", "InvoiceDate", "DueDate", "CreatedAt", "Status", "SubTotal", "Tax", "TaxRate", "Credit", "Total", "Currency")
            VALUES (@client, @now, @due, @now, 'Unpaid', 0, 0, 0, 0, 0, 'USD') RETURNING "Id"
            """,
            ("client", clientId),
            ("now", now),
            ("due", now.AddDays(7)));
    }

    /// <summary>Runs an insert that returns the new id.</summary>
    /// <param name="connectionString">The connection string of the test database.</param>
    /// <param name="sql">The insert, ending in <c>RETURNING "Id"</c>.</param>
    /// <param name="parameters">The named parameters.</param>
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
