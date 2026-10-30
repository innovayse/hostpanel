using System.Data.Common;
using System.Text.RegularExpressions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// The <c>PascalCaseDatabaseIdentifiers</c> migration renames every table, key, index and identity sequence in
/// place. These tests prove the two promises that matter: the names it renames are the names the earlier
/// migrations created (a wrong name makes the rename fail, and an identity sequence under a wrong name would be
/// skipped silently), and no row is lost or moved going down and up again.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed partial class PascalCaseRenameMigrationTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The last migration before the rename, the schema every database in production still has.</summary>
    private const string PreviousMigration = "20261021090000_AddServerLocations";

    /// <summary>
    /// The history table EF itself names, with its key (<c>PK___EFMigrationsHistory</c>): not ours to rename, and
    /// the one table whose rows legitimately change when a migration is applied or reverted.
    /// </summary>
    private const string HistoryTable = "__EFMigrationsHistory";

    /// <summary>
    /// A database built by the earlier migrations is renamed in full by the migration: it applies without error,
    /// and afterwards no table, index, sequence or constraint carries a lowercase or snake_case name.
    /// </summary>
    [Fact]
    public async Task Up_OnTheSchemaTheEarlierMigrationsCreate_LeavesOnlyPascalCaseNamesAsync()
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
            await using var context = new AppDbContext(options, new ModelOnlyEncryptionService());
            await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);

            var before = await RelationNamesAsync(connectionString);
            before.Should().Contain("invoice_items", "the schema before the rename is the snake_case one");

            await context.Database.MigrateAsync();

            var after = await RelationNamesAsync(connectionString);
            after.Where(name => !name.Contains(HistoryTable) && !PascalCase().IsMatch(name))
                .Should().BeEmpty("the rename migration leaves no lowercase or snake_case relation, key or sequence behind");
        }
        finally
        {
            using var probe = new NpgsqlConnection(connectionString);
            NpgsqlConnection.ClearPool(probe);
            await SharedPostgresServer.DropDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// On a database holding the demo data, going down and up again keeps every row: each table has the same
    /// count after <c>Down</c> and after <c>Up</c> as before, and <c>Up</c> restores exactly the names it started from.
    /// </summary>
    [Fact]
    public async Task DownThenUp_OnSeededDemoData_KeepsEveryRowAndRestoresTheNamesAsync()
    {
        var host = DemoTestHost.Enabled(factory);
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IDemoDataManager>().ResetAsync(CancellationToken.None);

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connectionString = context.Database.GetConnectionString()!;
        var migrator = context.GetService<IMigrator>();
        var rowsBefore = await RowCountsAsync(connectionString);
        var namesBefore = await RelationNamesAsync(connectionString);
        rowsBefore.Values.Sum().Should().BeGreaterThan(0, "the demo data was seeded");

        await migrator.MigrateAsync(PreviousMigration);

        var namesDown = await RelationNamesAsync(connectionString);
        namesDown.Should().Contain(["invoice_items", "PK_invoice_items", "invoice_items_Id_seq"], "Down restores the snake_case names");
        namesDown.Should().NotContain("InvoiceItems");

        // Tables created by migrations after the rename are dropped by going down past them, so they are not
        // expected in the Down snapshot; they are empty in the demo data, so nothing is lost.
        // Every later migration that creates a table must add it here, or going Down past it fails the comparison.
        var createdAfterTheRename = new[] { "invoicereminders", "serversshsettings", "sshkeys", "jobruns", "dataprotectionkeys", "fulfilmentrecords" };
        var rowsExpectedDown = rowsBefore.Where(row => !createdAfterTheRename.Contains(row.Key)).ToDictionary(row => row.Key, row => row.Value);
        (await RowCountsAsync(connectionString)).Should().Equal(rowsExpectedDown, "Down renames in place and copies nothing");

        await migrator.MigrateAsync();

        (await RowCountsAsync(connectionString)).Should().Equal(rowsBefore, "Up renames in place and copies nothing");
        (await RelationNamesAsync(connectionString)).Should().BeEquivalentTo(namesBefore, "Up restores exactly the names the model expects");
    }

    /// <summary>Counts the rows of every table in the <c>public</c> schema.</summary>
    /// <param name="connectionString">The database to read.</param>
    /// <returns>The row count of each table, keyed by its name with case and underscores removed, so the snake_case and PascalCase spellings of one table meet.</returns>
    private static async Task<Dictionary<string, long>> RowCountsAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var tables = await ReadStringsAsync(
            connection,
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE'");

        var counts = new Dictionary<string, long>();
        foreach (var table in tables.Where(table => table != HistoryTable))
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT count(*) FROM \"{table}\"";
            var count = (long)(await command.ExecuteScalarAsync())!;
            counts[table.Replace("_", string.Empty).ToLowerInvariant()] = count;
        }

        return counts;
    }

    /// <summary>Lists the name of every table, index, sequence and constraint in the <c>public</c> schema.</summary>
    /// <param name="connectionString">The database to read.</param>
    /// <returns>The distinct names.</returns>
    private static async Task<HashSet<string>> RelationNamesAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var relations = await ReadStringsAsync(
            connection,
            "SELECT c.relname FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace " +
            "WHERE n.nspname = 'public' AND c.relkind IN ('r', 'i', 'S')");
        var constraints = await ReadStringsAsync(
            connection,
            "SELECT c.conname FROM pg_constraint c JOIN pg_namespace n ON n.oid = c.connamespace WHERE n.nspname = 'public'");

        return [.. relations, .. constraints];
    }

    /// <summary>Runs a query that returns one text column.</summary>
    /// <param name="connection">An open connection.</param>
    /// <param name="sql">The query.</param>
    /// <returns>The first column of every row.</returns>
    private static async Task<List<string>> ReadStringsAsync(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync())
        {
            values.Add(reader.GetString(0));
        }

        return values;
    }

    /// <summary>
    /// PascalCase segments, optionally joined by single underscores as EF's <c>PK_</c>, <c>FK_</c>, <c>IX_</c> and
    /// <c>CK_</c> names and identity sequence names (<c>Invoices_Id_seq</c>) join them.
    /// </summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"^[A-Z][A-Za-z0-9]*(_[A-Za-z0-9]+)*$")]
    private static partial Regex PascalCase();
}
