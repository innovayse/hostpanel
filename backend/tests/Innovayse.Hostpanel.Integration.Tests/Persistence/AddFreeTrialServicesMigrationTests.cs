using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// <c>AddFreeTrialServices</c>: the trial columns on <c>ClientServices</c> and the <c>ClientProductTrials</c> table appear
/// on up and go on down, and a service stored before the migration keeps working with no trial.
/// </summary>
public sealed class AddFreeTrialServicesMigrationTests
{
    /// <summary>The migration before the one under test.</summary>
    private const string PreviousMigration = "20261123061243_MoveCwpServersToPlugin";

    /// <summary>The migration under test.</summary>
    private const string TargetMigration = "20261123125740_AddFreeTrialServices";

    /// <summary>Up creates the table and the columns.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Up_CreatesTheTableAndTheColumnsAsync()
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            await MigrateAsync(connectionString, TargetMigration);

            (await CountColumnsAsync(connectionString, "ClientProductTrials")).Should().Be(4);
            (await CountColumnsAsync(connectionString, "ClientServices", TrialColumns)).Should().Be(TrialColumns.Length);
        }
        finally
        {
            await SharedPostgresServer.DropDatabaseAsync(databaseName);
        }
    }

    /// <summary>Down removes the table and the columns.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Down_RemovesTheTableAndTheColumnsAsync()
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            await MigrateAsync(connectionString, TargetMigration);
            await MigrateAsync(connectionString, PreviousMigration);

            (await CountColumnsAsync(connectionString, "ClientProductTrials")).Should().Be(0);
            (await CountColumnsAsync(connectionString, "ClientServices", TrialColumns)).Should().Be(0);
        }
        finally
        {
            await SharedPostgresServer.DropDatabaseAsync(databaseName);
        }
    }

    /// <summary>A service stored before the migration loads with no trial and the default settings.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Up_OnAnExistingService_LeavesTheTrialFieldsEmptyAsync()
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            await MigrateAsync(connectionString, PreviousMigration);
            var serviceId = await SeedServiceAsync(connectionString);
            await MigrateAsync(connectionString, TargetMigration);

            await using var read = NewContext(connectionString);
            // Projected: the model has columns later migrations add, which this schema does not have yet.
            var service = await read.ClientServices.AsNoTracking().Where(s => s.Id == serviceId)
                .Select(s => new { s.TrialStatus, s.TrialEndsAt, s.TrialAutoConvert, s.TrialGracePeriodDays, s.TrialReminderDays })
                .SingleAsync();

            service.TrialStatus.Should().BeNull();
            service.TrialEndsAt.Should().BeNull();
            service.TrialAutoConvert.Should().BeFalse();
            service.TrialGracePeriodDays.Should().Be(0);
            service.TrialReminderDays.Should().BeNull();
        }
        finally
        {
            await SharedPostgresServer.DropDatabaseAsync(databaseName);
        }
    }

    /// <summary>The trial columns of <c>ClientServices</c>.</summary>
    private static readonly string[] TrialColumns =
        ["TrialStatus", "TrialEndsAt", "TrialAutoConvert", "TrialGracePeriodDays", "TrialReminderDays"];

    /// <summary>Opens a context over the database.</summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>A new context.</returns>
    private static AppDbContext NewContext(string connectionString) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options, new ModelOnlyEncryptionService());

    /// <summary>Migrates the database up or down to a migration.</summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="target">The migration to reach.</param>
    /// <returns>A task that completes when the database is there.</returns>
    private static async Task MigrateAsync(string connectionString, string target)
    {
        await using var context = NewContext(connectionString);
        await context.GetService<IMigrator>().MigrateAsync(target);
    }

    /// <summary>Counts the columns of a table, optionally only the named ones.</summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="table">The table.</param>
    /// <param name="columns">The columns to count, or none for all.</param>
    /// <returns>The number of matching columns.</returns>
    private static async Task<long> CountColumnsAsync(string connectionString, string table, string[]? columns = null)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT count(*) FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @table AND (@cols::text[] IS NULL OR column_name = ANY(@cols))
            """;
        command.Parameters.AddWithValue("table", table);
        command.Parameters.Add(new NpgsqlParameter("cols", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text) { Value = (object?)columns ?? DBNull.Value });
        return (long)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>Inserts a service row with foreign keys not enforced, naming only columns the earlier schema has.</summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The new service's id.</returns>
    private static async Task<int> SeedServiceAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SET session_replication_role = replica;
            INSERT INTO "ClientServices" ("ClientId", "ProductId", "BillingCycle", "Status", "FirstPaymentAmount", "RecurringAmount",
                "CreatedAt", "AutoRenew", "AutoTerminateEndOfCycle", "HostingResourceRevision", "OverrideAutoSuspend", "Quantity", "SshKeyRevision")
            VALUES (1, 1, 'Monthly', 'Active', 1, 1, @now, true, false, 0, false, 1, 0) RETURNING "Id"
            """;
        command.Parameters.AddWithValue("now", TestMoments.Seeded);
        return (int)(await command.ExecuteScalarAsync())!;
    }
}
