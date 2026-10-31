using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// <c>RoundQuoteTotalsOnce</c> on a database that holds quotes saved before the rounding fix: each stored total
/// becomes the unrounded sum of the discounted lines rounded once to the currency's minor unit, half away from
/// zero, the way a quote computes it now. Quotes without lines and totals already right are left alone.
/// </summary>
public sealed class RoundQuoteTotalsOnceMigrationTests
{
    /// <summary>The migration before the one under test: the schema every database has before the backfill.</summary>
    private const string PreviousMigration = "20261031073058_AddGatewayUnreferencedNotifiedAt";

    /// <summary>The migration under test.</summary>
    private const string TargetMigration = "20261031141424_RoundQuoteTotalsOnce";

    /// <summary>Every kind of stored total is recalculated or kept as the rule says, per currency exponent.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Up_OnQuotesSavedBeforeTheFix_RoundsEachTotalOnceAfterTheSumAsync()
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

            var clientId = await SeedClientAsync(connectionString);
            var fractions = await SeedQuoteAsync(connectionString, clientId, "USD", 0.0080m, ("A", 0.004m, 1, 0m), ("B", 0.004m, 1, 0m));
            var discounted = await SeedQuoteAsync(connectionString, clientId, "USD", 20.0010m, ("A", 10m, 3, 33.33m));
            var midpoint = await SeedQuoteAsync(connectionString, clientId, "usd", 10.0050m, ("A", 10.005m, 1, 0m));
            var yen = await SeedQuoteAsync(connectionString, clientId, "JPY", 1000.5000m, ("A", 1000.5m, 1, 0m));
            var dinar = await SeedQuoteAsync(connectionString, clientId, "KWD", 0.0009m, ("A", 0.0005m, 1, 0m), ("B", 0.0004m, 1, 0m));
            var right = await SeedQuoteAsync(connectionString, clientId, "USD", 25.0000m, ("A", 12.5m, 2, 0m));
            var empty = await SeedQuoteAsync(connectionString, clientId, "USD", 5.1234m);

            await using (var context = NewContext(options))
            {
                await context.GetService<IMigrator>().MigrateAsync(TargetMigration);
            }

            (await TotalOfAsync(connectionString, fractions)).Should().Be(0.01m, "two lines of 0.004 sum to 0.008 and round once");
            (await TotalOfAsync(connectionString, discounted)).Should().Be(20.00m);
            (await TotalOfAsync(connectionString, midpoint)).Should().Be(10.01m, "half rounds away from zero, in any letter case of the code");
            (await TotalOfAsync(connectionString, yen)).Should().Be(1001m, "a zero decimal currency rounds to whole units");
            (await TotalOfAsync(connectionString, dinar)).Should().Be(0.001m, "a three decimal currency keeps three decimals: 0.0009 rounds to 0.001");
            (await TotalOfAsync(connectionString, right)).Should().Be(25.00m, "a total that is already right stays");
            (await TotalOfAsync(connectionString, empty)).Should().Be(5.1234m, "a quote without lines keeps its stored total");

            await using (var context = NewContext(options))
            {
                await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            }

            (await TotalOfAsync(connectionString, fractions)).Should().Be(0.01m, "going down keeps the rounded totals");
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

    /// <summary>Inserts the client the quotes belong to, naming only columns the schema at this point has.</summary>
    /// <param name="connectionString">The connection string of the database.</param>
    /// <returns>The new client's id.</returns>
    private static Task<int> SeedClientAsync(string connectionString) =>
        InsertAsync(
            connectionString,
            """
            INSERT INTO "Clients" ("UserId", "FirstName", "LastName", "Status", "CreatedAt", "Currency",
                "AllowSso", "DisableCcProcessing", "LateFees", "MarketingOptIn", "NotifyAffiliate", "NotifyDomain",
                "NotifyGeneral", "NotifyInvoice", "NotifyProduct", "NotifySupport", "OverdueNotices",
                "SeparateInvoices", "StatusUpdate", "TaxExempt")
            VALUES (@user, 'Ada', 'Rounding', 'Active', @now, 'USD',
                false, false, false, false, false, false, false, false, false, false, false, false, false, false)
            RETURNING "Id"
            """,
            ("user", Guid.NewGuid().ToString()),
            ("now", DateTimeOffset.UtcNow));

    /// <summary>Inserts a quote with the given stored total and its lines.</summary>
    /// <param name="connectionString">The connection string of the database.</param>
    /// <param name="clientId">The client the quote is for.</param>
    /// <param name="currency">The currency code, in any case.</param>
    /// <param name="total">The total as stored before the fix.</param>
    /// <param name="lines">The lines: description, unit price, quantity and discount percent.</param>
    /// <returns>The new quote's id.</returns>
    private static async Task<int> SeedQuoteAsync(
        string connectionString, int clientId, string currency, decimal total, params (string Description, decimal UnitPrice, int Quantity, decimal Discount)[] lines)
    {
        var now = DateTimeOffset.UtcNow;
        var quoteId = await InsertAsync(
            connectionString,
            """
            INSERT INTO "Quotes" ("ClientId", "Subject", "Stage", "ExpiryDate", "CreatedAt", "Total", "Currency")
            VALUES (@client, 'Old quote', 'Draft', @expiry, @now, @total, @currency) RETURNING "Id"
            """,
            ("client", clientId),
            ("expiry", now.AddDays(7)),
            ("now", now),
            ("total", total),
            ("currency", currency));
        foreach (var line in lines)
        {
            await InsertAsync(
                connectionString,
                """
                INSERT INTO "QuoteItems" ("QuoteId", "Description", "UnitPrice", "Quantity", "DiscountPercent", "Taxed")
                VALUES (@quote, @description, @price, @quantity, @discount, false) RETURNING "Id"
                """,
                ("quote", quoteId),
                ("description", line.Description),
                ("price", line.UnitPrice),
                ("quantity", line.Quantity),
                ("discount", line.Discount));
        }

        return quoteId;
    }

    /// <summary>Reads the stored total of one quote.</summary>
    /// <param name="connectionString">The connection string of the database.</param>
    /// <param name="quoteId">The id of the quote.</param>
    /// <returns>The stored total.</returns>
    private static async Task<decimal> TotalOfAsync(string connectionString, int quoteId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"Total\" FROM \"Quotes\" WHERE \"Id\" = @id";
        command.Parameters.AddWithValue("id", quoteId);
        return (decimal)(await command.ExecuteScalarAsync())!;
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
