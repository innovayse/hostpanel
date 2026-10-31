using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// <c>AddTransactionSource</c> on a database that already holds a ledger: every row gets the source its
/// own data points to, the unique gateway payment index exists afterwards, and a ledger that holds two gateway
/// payments under one reference stops the migration with the rows named.
/// </summary>
public sealed class AddTransactionSourceMigrationTests
{
    /// <summary>The migration before the one under test.</summary>
    private const string PreviousMigration = "20261031073058_AddGatewayUnreferencedNotifiedAt";

    /// <summary>The migration under test.</summary>
    private const string MigrationUnderTest = "20261031123016_AddTransactionSource";

    /// <summary>Each existing ledger row is given the source its method, description, amounts and invoice say.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Up_BackfillsEveryExistingRowFromItsOwnDataAsync()
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
            await MigrateAsync(options, PreviousMigration);
            var now = DateTimeOffset.UtcNow;
            var clientId = await InsertClientAsync(connectionString, now);
            var importedInvoice = await InsertInvoiceAsync(connectionString, clientId, now, externalId: "src-1");
            var ownInvoice = await InsertInvoiceAsync(connectionString, clientId, now, externalId: null);

            var settled = await InsertTransactionAsync(connectionString, clientId, ownInvoice, "Invoice #1 Payment", "bank-gateway", "r-1", 10m, 0m, now);
            var handedBack = await InsertTransactionAsync(connectionString, clientId, null, "Gateway payment for Invoice #1 credited to balance", "bank-gateway", "r-2", 10m, 0m, now);
            var cardReturn = await InsertTransactionAsync(connectionString, clientId, null, "Card payment for Invoice #1 refunded", "Card", "r-3", 10m, 10m, now);
            var refund = await InsertTransactionAsync(connectionString, clientId, ownInvoice, "Invoice #1 Refund", "bank-gateway", "r-4", 0m, 5m, now);
            var byHand = await InsertTransactionAsync(connectionString, clientId, ownInvoice, "Invoice #1 Payment", "Manual", "r-5", 10m, 0m, now);
            var typed = await InsertTransactionAsync(connectionString, clientId, null, "Top-up", "Card", "r-6", 10m, 0m, now);
            var imported = await InsertTransactionAsync(connectionString, clientId, importedInvoice, "Invoice #9 Payment", "bank-gateway", "r-7", 10m, 0m, now);
            var noReference = await InsertTransactionAsync(connectionString, clientId, ownInvoice, "Invoice #1 Payment", "bank-gateway", string.Empty, 10m, 0m, now);

            await MigrateAsync(options, MigrationUnderTest);

            (await SourceOfAsync(connectionString, settled)).Should().Be("GatewayPayment");
            (await SourceOfAsync(connectionString, handedBack)).Should().Be("GatewayPayment");
            (await SourceOfAsync(connectionString, cardReturn)).Should().Be("GatewayPayment");
            (await SourceOfAsync(connectionString, refund)).Should().Be("GatewayRefund");
            (await SourceOfAsync(connectionString, byHand)).Should().Be("Manual");
            (await SourceOfAsync(connectionString, typed)).Should().Be("Manual");
            (await SourceOfAsync(connectionString, imported)).Should().Be("Import");
            (await SourceOfAsync(connectionString, noReference)).Should().Be("Manual");
            (await ScalarAsync(connectionString, "SELECT count(*) FROM pg_indexes WHERE indexname = 'IX_Transactions_PaymentMethod_TransactionId_GatewayPayment'"))
                .Should().Be(1L);

            // The empty default that let the column be added to a filled table is gone: a writer that
            // does not name the source fails instead of recording an empty one.
            (await ScalarAsync(connectionString, "SELECT column_default FROM information_schema.columns WHERE table_name = 'Transactions' AND column_name = 'Source'"))
                .Should().BeOfType<DBNull>();

            await MigrateAsync(options, PreviousMigration);

            (await ScalarAsync(connectionString, "SELECT count(*) FROM \"Transactions\"")).Should().Be(8L, "Down keeps every row");
        }
        finally
        {
            using var probe = new NpgsqlConnection(connectionString);
            NpgsqlConnection.ClearPool(probe);
            await SharedPostgresServer.DropDatabaseAsync(databaseName);
        }
    }

    /// <summary>Two gateway payments under one method and reference stop the migration, and the message names them.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Up_WithTwoGatewayPaymentsUnderOneReference_StopsAndNamesThemAsync()
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
            await MigrateAsync(options, PreviousMigration);
            var now = DateTimeOffset.UtcNow;
            var clientId = await InsertClientAsync(connectionString, now);
            var first = await InsertTransactionAsync(connectionString, clientId, null, "Invoice #1 Payment", "bank-gateway", "dup-1", 10m, 0m, now);
            var second = await InsertTransactionAsync(connectionString, clientId, null, "Gateway payment for Invoice #2 credited to balance", "bank-gateway", "dup-1", 10m, 0m, now);

            var act = () => MigrateAsync(options, MigrationUnderTest);

            var thrown = await act.Should().ThrowAsync<Exception>();
            thrown.Which.ToString().Should().Contain($"bank-gateway / dup-1 (ledger rows {first}, {second})");
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

    /// <summary>Reads the source of one ledger row.</summary>
    /// <param name="connectionString">The connection string of the test database.</param>
    /// <param name="id">The id of the row.</param>
    /// <returns>The row's source.</returns>
    private static async Task<string> SourceOfAsync(string connectionString, int id)
    {
        return (string)(await ScalarAsync(connectionString, $"SELECT \"Source\" FROM \"Transactions\" WHERE \"Id\" = {id}"))!;
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

    /// <summary>
    /// Inserts a client, naming only the columns the schema had before the migration under test.
    /// </summary>
    /// <param name="connectionString">The connection string of the test database.</param>
    /// <param name="now">The creation moment.</param>
    /// <returns>The client id.</returns>
    private static async Task<int> InsertClientAsync(string connectionString, DateTimeOffset now)
    {
        return await InsertAsync(
            connectionString,
            """
            INSERT INTO "Clients" ("UserId", "FirstName", "LastName", "Status", "CreatedAt",
                "AllowSso", "DisableCcProcessing", "LateFees", "MarketingOptIn", "NotifyAffiliate", "NotifyDomain",
                "NotifyGeneral", "NotifyInvoice", "NotifyProduct", "NotifySupport", "OverdueNotices",
                "SeparateInvoices", "StatusUpdate", "TaxExempt")
            VALUES (@user, 'Ada', 'Lovelace', 'Active', @now,
                false, false, false, false, false, false, false, false, false, false, false, false, false, false)
            RETURNING "Id"
            """,
            ("user", $"subject-{Guid.NewGuid():N}"),
            ("now", now));
    }

    /// <summary>Inserts an unpaid invoice, optionally carrying the id of the system it was imported from.</summary>
    /// <param name="connectionString">The connection string of the test database.</param>
    /// <param name="clientId">The owning client.</param>
    /// <param name="now">The creation moment.</param>
    /// <param name="externalId">The source system's id, or <see langword="null"/> for an invoice raised here.</param>
    /// <returns>The invoice id.</returns>
    private static async Task<int> InsertInvoiceAsync(string connectionString, int clientId, DateTimeOffset now, string? externalId)
    {
        return await InsertAsync(
            connectionString,
            """
            INSERT INTO "Invoices" ("ClientId", "InvoiceDate", "DueDate", "CreatedAt", "Status", "SubTotal", "Tax", "TaxRate", "Credit", "Total", "Currency", "ExternalId")
            VALUES (@client, @now, @due, @now, 'Unpaid', 0, 0, 0, 0, 0, 'USD', @external) RETURNING "Id"
            """,
            ("client", clientId),
            ("now", now),
            ("due", now.AddDays(7)),
            ("external", (object?)externalId ?? DBNull.Value));
    }

    /// <summary>Inserts one ledger row as the schema before the migration under test holds it.</summary>
    /// <param name="connectionString">The connection string of the test database.</param>
    /// <param name="clientId">The owning client.</param>
    /// <param name="invoiceId">The linked invoice, or <see langword="null"/>.</param>
    /// <param name="description">The description.</param>
    /// <param name="method">The payment method.</param>
    /// <param name="reference">The reference.</param>
    /// <param name="amountIn">The amount in.</param>
    /// <param name="amountOut">The amount out.</param>
    /// <param name="now">The moment of the row.</param>
    /// <returns>The row id.</returns>
    private static async Task<int> InsertTransactionAsync(
        string connectionString,
        int clientId,
        int? invoiceId,
        string description,
        string method,
        string reference,
        decimal amountIn,
        decimal amountOut,
        DateTimeOffset now)
    {
        return await InsertAsync(
            connectionString,
            """
            INSERT INTO "Transactions" ("ClientId", "Date", "Description", "TransactionId", "InvoiceId", "PaymentMethod",
                "AmountIn", "AmountOut", "Fees", "AddedToCredit", "Currency")
            VALUES (@client, @now, @description, @reference, @invoice, @method, @in, @out, 0, false, 'USD') RETURNING "Id"
            """,
            ("client", clientId),
            ("now", now),
            ("description", description),
            ("reference", reference),
            ("invoice", (object?)invoiceId ?? DBNull.Value),
            ("method", method),
            ("in", amountIn),
            ("out", amountOut));
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
