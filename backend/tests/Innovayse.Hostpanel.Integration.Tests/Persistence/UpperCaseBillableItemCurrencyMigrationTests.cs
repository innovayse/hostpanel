using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// <c>UpperCaseBillableItemCurrency</c> on a database that holds billable items saved with a lower case or padded
/// currency code: each code becomes upper case and trimmed, and a code that is already upper case stays as it is.
/// </summary>
public sealed class UpperCaseBillableItemCurrencyMigrationTests
{
    /// <summary>The migration before the one under test: the schema every database has before the data fix.</summary>
    private const string PreviousMigration = "20261031141424_RoundQuoteTotalsOnce";

    /// <summary>The migration under test.</summary>
    private const string TargetMigration = "20261031204337_UpperCaseBillableItemCurrency";

    /// <summary>A lower case code becomes upper case, an upper case code stays, and going down keeps the data.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Up_OnLowerCaseCurrency_UpperCasesItAndKeepsUpperCaseAsync()
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
            var lower = await SeedItemAsync(connectionString, clientId, "usd");
            var padded = await SeedItemAsync(connectionString, clientId, " eu");
            var upper = await SeedItemAsync(connectionString, clientId, "EUR");

            await using (var context = NewContext(options))
            {
                await context.GetService<IMigrator>().MigrateAsync(TargetMigration);
            }

            (await CurrencyOfAsync(connectionString, lower)).Should().Be("USD", "a lower case code becomes upper case");
            (await CurrencyOfAsync(connectionString, padded)).Should().Be("EU", "a padded code is trimmed and upper cased");
            (await CurrencyOfAsync(connectionString, upper)).Should().Be("EUR", "a code that is already upper case stays");

            await using (var context = NewContext(options))
            {
                await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            }

            (await CurrencyOfAsync(connectionString, lower)).Should().Be("USD", "going down keeps the upper case codes");
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

    /// <summary>Inserts the client the items belong to, naming only columns the schema at this point has.</summary>
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
            VALUES (@user, 'Ada', 'Billable', 'Active', @now, 'USD',
                false, false, false, false, false, false, false, false, false, false, false, false, false, false)
            RETURNING "Id"
            """,
            ("user", Guid.NewGuid().ToString()),
            ("now", DateTimeOffset.UtcNow));

    /// <summary>Inserts an uninvoiced billable item with the given stored currency code.</summary>
    /// <param name="connectionString">The connection string of the database.</param>
    /// <param name="clientId">The client the item is for.</param>
    /// <param name="currency">The currency code exactly as stored.</param>
    /// <returns>The new item's id.</returns>
    private static Task<int> SeedItemAsync(string connectionString, int clientId, string currency) =>
        InsertAsync(
            connectionString,
            """
            INSERT INTO "BillableItems" ("ClientId", "Description", "Amount", "Currency", "Type", "IsInvoiced", "CreatedAt")
            VALUES (@client, 'Old item', 10, @currency, 'OneTime', false, @now) RETURNING "Id"
            """,
            ("client", clientId),
            ("currency", currency),
            ("now", DateTimeOffset.UtcNow));

    /// <summary>Reads the stored currency code of one billable item.</summary>
    /// <param name="connectionString">The connection string of the database.</param>
    /// <param name="itemId">The id of the item.</param>
    /// <returns>The stored currency code.</returns>
    private static async Task<string> CurrencyOfAsync(string connectionString, int itemId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"Currency\" FROM \"BillableItems\" WHERE \"Id\" = @id";
        command.Parameters.AddWithValue("id", itemId);
        return (string)(await command.ExecuteScalarAsync())!;
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
