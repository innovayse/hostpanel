using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// <c>MoveCwpServersToPlugin</c> on a database whose servers were served by the host's own Control Web Panel provider:
/// the servers are named for the module the plugin serves, the plugin is switched on, and the stored credentials are untouched.
/// </summary>
public sealed class MoveCwpServersToPluginMigrationTests
{
    /// <summary>The migration before the one under test.</summary>
    private const string PreviousMigration = "20261122011734_AddInvoiceGatewayReversalNotifiedAt";

    /// <summary>The migration under test.</summary>
    private const string TargetMigration = "20261123061243_MoveCwpServersToPlugin";

    /// <summary>The key of the plugin's switch.</summary>
    private const string SwitchKey = "integration:cwp:is_enabled";

    /// <summary>A legacy panel server becomes the plugin's module, the plugin is switched on, and other servers and credentials stay.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Up_OnLegacyPanelServers_NamesThePluginModuleAndSwitchesItOnAsync()
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
            await MigrateToAsync(options, PreviousMigration);

            var current = await SeedServerAsync(connectionString, "Cwp7", "hash-current");
            var legacy = await SeedServerAsync(connectionString, "Centos", "hash-legacy");
            var other = await SeedServerAsync(connectionString, "CPanel", "hash-other");

            await MigrateToAsync(options, TargetMigration);

            (await ModuleOfAsync(connectionString, current)).Should().Be("Cwp7");
            (await ModuleOfAsync(connectionString, legacy)).Should().Be("Cwp7", "the old panel module is the same panel the plugin serves");
            (await ModuleOfAsync(connectionString, other)).Should().Be("CPanel", "a server of another module is not touched");
            (await HashOfAsync(connectionString, legacy)).Should().Be("hash-legacy", "the plugin reads the stored API key where it already is");
            (await SwitchOfAsync(connectionString)).Should().Be("true");
        }
        finally
        {
            await SharedPostgresServer.DropDatabaseAsync(databaseName);
        }
    }

    /// <summary>An operator's own choice for the switch is kept, and a run on the migrated data changes nothing.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Up_OnExistingSwitchAndRerun_KeepsTheStoredValueAsync()
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
            await MigrateToAsync(options, PreviousMigration);

            await SeedServerAsync(connectionString, "Cwp7", "hash-current");
            await ExecuteAsync(connectionString, $"""INSERT INTO "Settings" ("Key", "Value") VALUES ('{SwitchKey}', 'false')""");

            await MigrateToAsync(options, TargetMigration);
            await MigrateToAsync(options, PreviousMigration);
            await MigrateToAsync(options, TargetMigration);

            (await SwitchOfAsync(connectionString)).Should().Be("false", "the operator switched the plugin off, the migration does not overrule it");
            (await CountAsync(connectionString, "Servers")).Should().Be(1);
        }
        finally
        {
            await SharedPostgresServer.DropDatabaseAsync(databaseName);
        }
    }

    /// <summary>A deployment with no legacy panel server gets no switch row.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Up_WithoutPanelServers_LeavesThePluginSwitchAbsentAsync()
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
            await MigrateToAsync(options, PreviousMigration);

            await SeedServerAsync(connectionString, "CPanel", "hash-other");

            await MigrateToAsync(options, TargetMigration);

            (await SwitchOfAsync(connectionString)).Should().BeNull("nothing on this deployment is served by the plugin");
        }
        finally
        {
            await SharedPostgresServer.DropDatabaseAsync(databaseName);
        }
    }

    /// <summary>Migrates the database to the given migration, up or down.</summary>
    /// <param name="options">The context options naming the database.</param>
    /// <param name="migration">The migration to end on.</param>
    /// <returns>A task that completes when the database is at that migration.</returns>
    private static async Task MigrateToAsync(DbContextOptions<AppDbContext> options, string migration)
    {
        await using var context = new AppDbContext(options, new ModelOnlyEncryptionService());
        await context.GetService<IMigrator>().MigrateAsync(migration);
    }

    /// <summary>Inserts a server, naming only columns the schema before the migration has.</summary>
    /// <param name="connectionString">The connection string of the database.</param>
    /// <param name="module">The stored module text.</param>
    /// <param name="accessHash">The stored API key.</param>
    /// <returns>The new server's id.</returns>
    private static async Task<int> SeedServerAsync(string connectionString, string module, string accessHash)
    {
        return (int)(await ScalarAsync(
            connectionString,
            """
            INSERT INTO "Servers" ("Name", "Hostname", "Module", "Username", "AccessHash", "UseSSL", "IsDefault", "IsDisabled",
                "AcceptsSelfSignedCertificate", "MonthlyCost", "CreatedAt")
            VALUES (@name, 'panel.example.test', @module, 'root', @hash, true, false, false, false, 0, @now) RETURNING "Id"
            """,
            ("name", $"server-{Guid.NewGuid():N}"),
            ("module", module),
            ("hash", accessHash),
            ("now", TestMoments.Seeded)))!;
    }

    /// <summary>Reads a server's stored module.</summary>
    /// <param name="connectionString">The connection string of the database.</param>
    /// <param name="id">The server's id.</param>
    /// <returns>The module text.</returns>
    private static async Task<string?> ModuleOfAsync(string connectionString, int id) =>
        (string?)await ScalarAsync(connectionString, """SELECT "Module" FROM "Servers" WHERE "Id" = @id""", ("id", id));

    /// <summary>Reads a server's stored API key.</summary>
    /// <param name="connectionString">The connection string of the database.</param>
    /// <param name="id">The server's id.</param>
    /// <returns>The stored API key.</returns>
    private static async Task<string?> HashOfAsync(string connectionString, int id) =>
        (string?)await ScalarAsync(connectionString, """SELECT "AccessHash" FROM "Servers" WHERE "Id" = @id""", ("id", id));

    /// <summary>Reads the plugin's switch.</summary>
    /// <param name="connectionString">The connection string of the database.</param>
    /// <returns>The stored value, or <see langword="null"/> when the row is absent.</returns>
    private static async Task<string?> SwitchOfAsync(string connectionString) =>
        (string?)await ScalarAsync(connectionString, """SELECT "Value" FROM "Settings" WHERE "Key" = @key""", ("key", SwitchKey));

    /// <summary>Counts the rows of a table.</summary>
    /// <param name="connectionString">The connection string of the database.</param>
    /// <param name="table">The table name, a constant of this test.</param>
    /// <returns>The number of rows.</returns>
    private static async Task<long> CountAsync(string connectionString, string table) =>
        (long)(await ScalarAsync(connectionString, $"""SELECT count(*) FROM "{table}" """))!;

    /// <summary>Runs one statement that returns nothing.</summary>
    /// <param name="connectionString">The connection string of the database.</param>
    /// <param name="sql">The statement.</param>
    /// <returns>A task that completes when the statement has run.</returns>
    private static async Task ExecuteAsync(string connectionString, string sql) => await ScalarAsync(connectionString, sql);

    /// <summary>Runs one statement and returns its first value.</summary>
    /// <param name="connectionString">The connection string of the database.</param>
    /// <param name="sql">The statement.</param>
    /// <param name="parameters">The statement's parameters.</param>
    /// <returns>The first value, or <see langword="null"/> when there is none.</returns>
    private static async Task<object?> ScalarAsync(string connectionString, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        var result = await command.ExecuteScalarAsync();
        return result is DBNull ? null : result;
    }
}
