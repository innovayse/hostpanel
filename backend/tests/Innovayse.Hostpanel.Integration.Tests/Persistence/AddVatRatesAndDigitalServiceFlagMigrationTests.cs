using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// <c>AddVatRatesAndDigitalServiceFlag</c> on a database that already holds invoice lines with a product snapshot: the new
/// snapshot column is filled for them, so the line loads (a non-nullable flag cannot be read from NULL), and a line with no
/// snapshot still loads as one.
/// </summary>
public sealed class AddVatRatesAndDigitalServiceFlagMigrationTests
{
    /// <summary>The migration before the one under test: the schema every database has before the flag exists.</summary>
    private const string PreviousMigration = "20261115001832_TightenProductOptionQuantityRange";

    /// <summary>The migration under test.</summary>
    private const string TargetMigration = "20261116094807_AddVatRatesAndDigitalServiceFlag";

    /// <summary>A line snapshotted before the flag existed loads with the flag off, and a line with no snapshot has none.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Up_OnLinesWithAndWithoutASnapshot_LoadsBothAsync()
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
            await using (var context = NewContext(options))
            {
                await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            }

            var invoiceId = await SeedInvoiceAsync(connectionString);
            var snapshotted = await SeedItemAsync(connectionString, invoiceId, withSnapshot: true);
            var plain = await SeedItemAsync(connectionString, invoiceId, withSnapshot: false);

            await using (var context = NewContext(options))
            {
                await context.GetService<IMigrator>().MigrateAsync(TargetMigration);
            }

            await using var read = NewContext(options);
            var items = await read.InvoiceItems.AsNoTracking().Where(i => i.InvoiceId == invoiceId).ToDictionaryAsync(i => i.Id);

            items[snapshotted].ProductSnapshot.Should().NotBeNull("the line had a snapshot before the flag existed");
            items[snapshotted].ProductSnapshot!.IsDigitalService.Should().BeFalse("such a line was never taxed as a digital service");
            items[snapshotted].ProductSnapshot!.GracePeriodDays.Should().Be(14, "the rest of the snapshot is untouched");
            items[plain].ProductSnapshot.Should().BeNull("a line with no snapshot has none after the migration either");
        }
        finally
        {
            await SharedPostgresServer.DropDatabaseAsync(databaseName);
        }
    }

    /// <summary>Opens a context over the database.</summary>
    /// <param name="options">The context options naming the database.</param>
    /// <returns>A new context.</returns>
    private static AppDbContext NewContext(DbContextOptions<AppDbContext> options) =>
        new(options, new ModelOnlyEncryptionService());

    /// <summary>Inserts a client and an invoice for it, naming only columns the schema before the migration has.</summary>
    /// <param name="connectionString">The connection string of the database.</param>
    /// <returns>The new invoice's id.</returns>
    private static async Task<int> SeedInvoiceAsync(string connectionString)
    {
        var clientId = await InsertAsync(
            connectionString,
            """
            INSERT INTO "Clients" ("UserId", "FirstName", "LastName", "Status", "CreatedAt", "Currency",
                "AllowSso", "DisableCcProcessing", "LateFees", "MarketingOptIn", "NotifyAffiliate", "NotifyDomain",
                "NotifyGeneral", "NotifyInvoice", "NotifyProduct", "NotifySupport", "OverdueNotices",
                "SeparateInvoices", "StatusUpdate", "TaxExempt")
            VALUES (@user, 'Ada', 'Snapshot', 'Active', @now, 'USD',
                false, false, false, false, false, false, false, false, false, false, false, false, false, false)
            RETURNING "Id"
            """,
            ("user", Guid.NewGuid().ToString()),
            ("now", TestMoments.Seeded));

        return await InsertAsync(
            connectionString,
            """
            INSERT INTO "Invoices" ("ClientId", "InvoiceDate", "DueDate", "CreatedAt", "Status", "SubTotal", "Tax", "TaxRate", "Credit", "Total", "Currency")
            VALUES (@client, @now, @due, @now, 'Unpaid', 0, 0, 0, 0, 0, 'USD') RETURNING "Id"
            """,
            ("client", clientId),
            ("now", TestMoments.Seeded),
            ("due", TestMoments.Seeded.AddDays(7)));
    }

    /// <summary>Inserts an invoice line, with or without the product snapshot columns the schema before the migration has.</summary>
    /// <param name="connectionString">The connection string of the database.</param>
    /// <param name="invoiceId">The invoice the line belongs to.</param>
    /// <param name="withSnapshot">Whether the snapshot columns are filled.</param>
    /// <returns>The new line's id.</returns>
    private static Task<int> SeedItemAsync(string connectionString, int invoiceId, bool withSnapshot) =>
        InsertAsync(
            connectionString,
            withSnapshot
                ? """
                INSERT INTO "InvoiceItems" ("InvoiceId", "Description", "UnitPrice", "Quantity", "Amount", "ProductId", "BillingCycle", "IsDigital", "GracePeriodDays")
                VALUES (@invoice, 'Hosting', 10, 1, 10, 1, 'Monthly', false, 14) RETURNING "Id"
                """
                : """
                INSERT INTO "InvoiceItems" ("InvoiceId", "Description", "UnitPrice", "Quantity", "Amount")
                VALUES (@invoice, 'Manual line', 10, 1, 10) RETURNING "Id"
                """,
            ("invoice", invoiceId));

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
