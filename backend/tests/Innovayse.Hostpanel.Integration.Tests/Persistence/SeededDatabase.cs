using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// A throwaway database migrated to the schema before the backfill and seeded with rows whose currency is empty,
/// dropped when disposed.
/// </summary>
internal sealed class SeededDatabase : IAsyncDisposable
{
    /// <summary>The migration that added the columns, the schema every production database has before the backfill.</summary>
    private const string PreviousMigration = "20261025175216_AddCurrencyToOrdersQuotesTransactions";

    /// <summary>The backfill migration under test.</summary>
    private const string BackfillMigration = "20261025185902_BackfillOrderQuoteTransactionCurrency";

    /// <summary>The unique name of the database, for the drop.</summary>
    private readonly string _name;

    /// <summary>The connection string of the database.</summary>
    private readonly string _connectionString;

    /// <summary>Initializes a new instance of the <see cref="SeededDatabase"/> class.</summary>
    /// <param name="name">The unique name of the database.</param>
    /// <param name="connectionString">The connection string of the database.</param>
    private SeededDatabase(string name, string connectionString)
    {
        _name = name;
        _connectionString = connectionString;
    }

    /// <summary>Gets the id of the transaction on an invoice whose client also has a currency.</summary>
    public int TransactionOnInvoice { get; private set; }

    /// <summary>Gets the id of the transaction without an invoice, of the client that has a currency.</summary>
    public int TransactionOfCurrencyClient { get; private set; }

    /// <summary>Gets the id of the transaction without an invoice, of the client that has no currency.</summary>
    public int TransactionOfBareClient { get; private set; }

    /// <summary>Gets the id of the transaction that already carries a currency.</summary>
    public int TransactionStamped { get; private set; }

    /// <summary>Gets the id of the order on an invoice whose client also has a currency.</summary>
    public int OrderOnInvoice { get; private set; }

    /// <summary>Gets the id of the order without an invoice, of the client that has a currency.</summary>
    public int OrderOfCurrencyClient { get; private set; }

    /// <summary>Gets the id of the order without an invoice, of the client that has no currency.</summary>
    public int OrderOfBareClient { get; private set; }

    /// <summary>Gets the id of the order that already carries a currency.</summary>
    public int OrderStamped { get; private set; }

    /// <summary>Gets the id of the quote of the client that has a currency.</summary>
    public int QuoteOfCurrencyClient { get; private set; }

    /// <summary>Gets the id of the quote of the client that has no currency.</summary>
    public int QuoteOfBareClient { get; private set; }

    /// <summary>Gets the id of the quote that already carries a currency.</summary>
    public int QuoteStamped { get; private set; }

    /// <summary>Creates the database, migrates it to the schema before the backfill and seeds it.</summary>
    /// <returns>The seeded database.</returns>
    public static async Task<SeededDatabase> CreateAsync()
    {
        var name = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(name);
        var database = new SeededDatabase(name, connectionString);
        try
        {
            await using var context = database.NewContext();
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            await database.SeedAsync();
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }

        return database;
    }

    /// <summary>Applies the backfill migration.</summary>
    /// <returns>A task that completes when the migration has run.</returns>
    public async Task MigrateToBackfillAsync()
    {
        await using var context = NewContext();
        await context.GetService<IMigrator>().MigrateAsync(BackfillMigration);
    }

    /// <summary>Reverts the backfill migration.</summary>
    /// <returns>A task that completes when the migration has been reverted.</returns>
    public async Task MigrateDownAsync()
    {
        await using var context = NewContext();
        await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
    }

    /// <summary>Reads the currency of one row.</summary>
    /// <param name="table">The table, one of the three the backfill fills.</param>
    /// <param name="id">The id of the row.</param>
    /// <returns>The row's currency.</returns>
    public async Task<string> CurrencyOfAsync(string table, int id)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT \"Currency\" FROM \"{table}\" WHERE \"Id\" = @id";
        command.Parameters.AddWithValue("id", id);
        return (string)(await command.ExecuteScalarAsync())!;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        using var probe = new NpgsqlConnection(_connectionString);
        NpgsqlConnection.ClearPool(probe);
        await SharedPostgresServer.DropDatabaseAsync(_name);
    }

    /// <summary>Opens a context over the database.</summary>
    /// <returns>A new context.</returns>
    private AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_connectionString).Options, new ModelOnlyEncryptionService());

    /// <summary>
    /// Seeds two clients (one with a currency, one without), an invoice in a third currency, and an order, a
    /// quote and a transaction per case, every row but the stamped ones with an empty currency: the state the
    /// column migration leaves existing rows in.
    /// </summary>
    /// <remarks>
    /// The rows are written in SQL naming only the columns the schema had before the backfill. Saving entities
    /// would write every column the model has today, and a column a later migration adds does not exist yet
    /// in this older schema.
    /// </remarks>
    /// <returns>A task that completes when the rows are saved.</returns>
    private async Task SeedAsync()
    {
        var now = TestMoments.Seeded;
        var withCurrency = await InsertClientAsync("Ada", BackfillOrderQuoteTransactionCurrencyMigrationTests.ClientCurrency, now);
        var bare = await InsertClientAsync("Bob", null, now);
        var invoice = await InsertAsync(
            """
            INSERT INTO "Invoices" ("ClientId", "InvoiceDate", "DueDate", "CreatedAt", "Status", "SubTotal", "Tax", "TaxRate", "Credit", "Total", "Currency")
            VALUES (@client, @now, @due, @now, 'Unpaid', 0, 0, 0, 0, 0, @currency) RETURNING "Id"
            """,
            ("client", withCurrency),
            ("now", now),
            ("due", now.AddDays(7)),
            ("currency", BackfillOrderQuoteTransactionCurrencyMigrationTests.InvoiceCurrency));

        var stamped = BackfillOrderQuoteTransactionCurrencyMigrationTests.StampedCurrency;
        TransactionOnInvoice = await InsertTransactionAsync(withCurrency, "onInvoice", invoice, string.Empty, now);
        TransactionOfCurrencyClient = await InsertTransactionAsync(withCurrency, "ofCurrencyClient", null, string.Empty, now);
        TransactionOfBareClient = await InsertTransactionAsync(bare, "ofBareClient", null, string.Empty, now);
        TransactionStamped = await InsertTransactionAsync(withCurrency, "stamped", null, stamped, now);

        // The order links to the invoice through the payment flow; here the link is written directly.
        OrderOnInvoice = await InsertOrderAsync("BF-1", withCurrency, invoice, string.Empty, now);
        OrderOfCurrencyClient = await InsertOrderAsync("BF-2", withCurrency, null, string.Empty, now);
        OrderOfBareClient = await InsertOrderAsync("BF-3", bare, null, string.Empty, now);
        OrderStamped = await InsertOrderAsync("BF-4", withCurrency, null, stamped, now);

        QuoteOfCurrencyClient = await InsertQuoteAsync(withCurrency, "Of the currency client", string.Empty, now);
        QuoteOfBareClient = await InsertQuoteAsync(bare, "Of the bare client", string.Empty, now);
        QuoteStamped = await InsertQuoteAsync(withCurrency, "Stamped", stamped, now);
    }

    /// <summary>Inserts an active client with every notification and billing flag off.</summary>
    /// <param name="firstName">The client's first name.</param>
    /// <param name="currency">The client's currency, or <see langword="null"/> for none.</param>
    /// <param name="now">When the client was created.</param>
    /// <returns>The new client's id.</returns>
    private Task<int> InsertClientAsync(string firstName, string? currency, DateTimeOffset now) =>
        InsertAsync(
            """
            INSERT INTO "Clients" ("UserId", "FirstName", "LastName", "Status", "CreatedAt", "Currency",
                "AllowSso", "DisableCcProcessing", "LateFees", "MarketingOptIn", "NotifyAffiliate", "NotifyDomain",
                "NotifyGeneral", "NotifyInvoice", "NotifyProduct", "NotifySupport", "OverdueNotices",
                "SeparateInvoices", "StatusUpdate", "TaxExempt")
            VALUES (@user, @first, 'Backfill', 'Active', @now, @currency,
                false, false, false, false, false, false, false, false, false, false, false, false, false, false)
            RETURNING "Id"
            """,
            ("user", Guid.NewGuid().ToString()),
            ("first", firstName),
            ("now", now),
            ("currency", currency));

    /// <summary>Inserts a manual payment transaction.</summary>
    /// <param name="clientId">The client the transaction belongs to.</param>
    /// <param name="key">A unique suffix for the gateway transaction id.</param>
    /// <param name="invoiceId">The invoice it settles, if any.</param>
    /// <param name="currency">The currency it carries, empty for one the backfill must fill.</param>
    /// <param name="now">When it was made.</param>
    /// <returns>The new transaction's id.</returns>
    private Task<int> InsertTransactionAsync(int clientId, string key, int? invoiceId, string currency, DateTimeOffset now) =>
        InsertAsync(
            """
            INSERT INTO "Transactions" ("ClientId", "Date", "Description", "TransactionId", "InvoiceId", "PaymentMethod", "AmountIn", "AmountOut", "Currency")
            VALUES (@client, @now, @key, @reference, @invoice, 'Manual', 10, 0, @currency) RETURNING "Id"
            """,
            ("client", clientId),
            ("now", now),
            ("key", key),
            ("reference", $"backfill-{key}"),
            ("invoice", invoiceId),
            ("currency", currency));

    /// <summary>Inserts a pending order.</summary>
    /// <param name="number">The order number.</param>
    /// <param name="clientId">The client who placed it.</param>
    /// <param name="invoiceId">The invoice it is billed on, if any.</param>
    /// <param name="currency">The currency it carries, empty for one the backfill must fill.</param>
    /// <param name="now">When it was placed.</param>
    /// <returns>The new order's id.</returns>
    private Task<int> InsertOrderAsync(string number, int clientId, int? invoiceId, string currency, DateTimeOffset now) =>
        InsertAsync(
            """
            INSERT INTO "Orders" ("OrderNumber", "ClientId", "Status", "PaymentMethod", "InvoiceId", "CreatedAt", "PaymentToken", "Currency")
            VALUES (@number, @client, 'Pending', 'Manual', @invoice, @now, @token, @currency) RETURNING "Id"
            """,
            ("number", number),
            ("client", clientId),
            ("invoice", invoiceId),
            ("now", now),
            ("token", Guid.NewGuid().ToString("N")),
            ("currency", currency));

    /// <summary>Inserts a draft quote.</summary>
    /// <param name="clientId">The client it is for.</param>
    /// <param name="subject">The quote subject.</param>
    /// <param name="currency">The currency it carries, empty for one the backfill must fill.</param>
    /// <param name="now">When it was created.</param>
    /// <returns>The new quote's id.</returns>
    private Task<int> InsertQuoteAsync(int clientId, string subject, string currency, DateTimeOffset now) =>
        InsertAsync(
            """
            INSERT INTO "Quotes" ("ClientId", "Subject", "Stage", "ExpiryDate", "CreatedAt", "Total", "Currency")
            VALUES (@client, @subject, 'Draft', @expiry, @now, 0, @currency) RETURNING "Id"
            """,
            ("client", clientId),
            ("subject", subject),
            ("expiry", now.AddDays(7)),
            ("now", now),
            ("currency", currency));

    /// <summary>Runs one insert that returns the new row's id.</summary>
    /// <param name="sql">The statement, ending in <c>RETURNING "Id"</c>.</param>
    /// <param name="parameters">The statement's parameters; a <see langword="null"/> value is written as SQL NULL.</param>
    /// <returns>The new row's id.</returns>
    private async Task<int> InsertAsync(string sql, params (string Name, object? Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return (int)(await command.ExecuteScalarAsync())!;
    }
}
