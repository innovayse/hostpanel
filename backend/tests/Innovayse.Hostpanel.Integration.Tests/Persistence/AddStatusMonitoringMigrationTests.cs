using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// <c>AddStatusMonitoring</c> on a database that already holds status components: a component an operator had set to a
/// state by hand keeps it as its override, an operational one gets no override, and going down keeps every row.
/// </summary>
public sealed class AddStatusMonitoringMigrationTests
{
    /// <summary>The migration before the one under test.</summary>
    private const string PreviousMigration = "20261112104836_AddTaxRulesAndClientTaxFields";

    /// <summary>The migration under test.</summary>
    private const string MigrationUnderTest = "20261112150830_AddStatusMonitoring";

    /// <summary>Each component that was not operational gets its state as the override, and operational ones get none.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Up_CopiesAManualStateIntoTheOverrideAndLeavesOperationalOnesAloneAsync()
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
            await MigrateAsync(options, PreviousMigration);
            await ExecuteAsync(connectionString, "INSERT INTO \"StatusComponentGroups\" (\"Id\", \"Name\", \"DisplayOrder\") OVERRIDING SYSTEM VALUE VALUES (1, 'Hosting', 0)");
            await InsertComponentAsync(connectionString, 1, "Operational");
            await InsertComponentAsync(connectionString, 2, "Outage");
            await InsertComponentAsync(connectionString, 3, "Maintenance");

            await MigrateAsync(options, MigrationUnderTest);

            (await OverrideOfAsync(connectionString, 1)).Should().BeOfType<DBNull>();
            (await OverrideOfAsync(connectionString, 2)).Should().Be("Outage");
            (await OverrideOfAsync(connectionString, 3)).Should().Be("Maintenance");

            await MigrateAsync(options, PreviousMigration);

            (await ScalarAsync(connectionString, "SELECT count(*) FROM \"StatusComponents\"")).Should().Be(3L, "Down keeps every row");
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

    /// <summary>Inserts a component in the first group, naming only the columns the schema had before the migration.</summary>
    /// <param name="connectionString">The connection string of the test database.</param>
    /// <param name="id">The component id.</param>
    /// <param name="state">The stored current state.</param>
    /// <returns>A task that completes when the row is saved.</returns>
    private static async Task InsertComponentAsync(string connectionString, int id, string state)
    {
        await ExecuteAsync(
            connectionString,
            $"INSERT INTO \"StatusComponents\" (\"Id\", \"GroupId\", \"Name\", \"DisplayOrder\", \"CurrentState\") OVERRIDING SYSTEM VALUE VALUES ({id}, 1, 'Component {id}', {id}, '{state}')");
    }

    /// <summary>Reads the override of one component.</summary>
    /// <param name="connectionString">The connection string of the test database.</param>
    /// <param name="id">The component id.</param>
    /// <returns>The stored override, or a database null.</returns>
    private static async Task<object?> OverrideOfAsync(string connectionString, int id)
    {
        return await ScalarAsync(connectionString, $"SELECT \"StateOverride\" FROM \"StatusComponents\" WHERE \"Id\" = {id}");
    }

    /// <summary>Runs a statement that returns nothing.</summary>
    /// <param name="connectionString">The connection string of the test database.</param>
    /// <param name="sql">The statement.</param>
    /// <returns>A task that completes when the statement has run.</returns>
    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
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
}
