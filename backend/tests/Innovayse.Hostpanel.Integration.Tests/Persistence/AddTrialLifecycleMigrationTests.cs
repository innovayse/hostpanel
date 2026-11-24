using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// <c>AddTrialLifecycle</c>: the <c>TrialReminders</c> table, its index and the <c>TrialPaymentDueAt</c> column appear on up
/// and go on down, and a trial stored before the migration keeps its values.
/// </summary>
public sealed class AddTrialLifecycleMigrationTests
{
    /// <summary>The migration before the one under test.</summary>
    private const string PreviousMigration = "20261126183638_AddDomainExtendedAttributes";

    /// <summary>The migration under test.</summary>
    private const string TargetMigration = "20261127100429_AddTrialLifecycle";

    /// <summary>Up creates the table, the index and the column.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Up_CreatesTheTableTheIndexAndTheColumnAsync()
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            await MigrateAsync(connectionString, TargetMigration);

            (await ScalarAsync(connectionString, """SELECT count(*) FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'TrialReminders'""")).Should().Be(4);
            (await ScalarAsync(connectionString, """SELECT count(*) FROM pg_indexes WHERE tablename = 'TrialReminders' AND indexname = 'IX_TrialReminders_ServiceId_DaysBefore'""")).Should().Be(1);
            (await ScalarAsync(connectionString, """SELECT count(*) FROM information_schema.columns WHERE table_name = 'ClientServices' AND column_name = 'TrialPaymentDueAt'""")).Should().Be(1);
        }
        finally
        {
            await SharedPostgresServer.DropDatabaseAsync(databaseName);
        }
    }

    /// <summary>Down removes the table and the column.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Down_RemovesTheTableAndTheColumnAsync()
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            await MigrateAsync(connectionString, TargetMigration);
            await MigrateAsync(connectionString, PreviousMigration);

            (await ScalarAsync(connectionString, """SELECT count(*) FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'TrialReminders'""")).Should().Be(0);
            (await ScalarAsync(connectionString, """SELECT count(*) FROM information_schema.columns WHERE table_name = 'ClientServices' AND column_name = 'TrialPaymentDueAt'""")).Should().Be(0);
        }
        finally
        {
            await SharedPostgresServer.DropDatabaseAsync(databaseName);
        }
    }

    /// <summary>A trial stored before the migration keeps its values, with no payment due date.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Up_OnAnExistingTrial_KeepsItsValuesAsync()
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            await MigrateAsync(connectionString, PreviousMigration);
            var serviceId = await SeedTrialAsync(connectionString);
            await MigrateAsync(connectionString, TargetMigration);

            await using var read = NewContext(connectionString);
            var service = await read.ClientServices.AsNoTracking().SingleAsync(s => s.Id == serviceId);

            service.TrialStatus.Should().Be(TrialStatus.Active);
            service.TrialEndsAt.Should().Be(TestMoments.Seeded.AddDays(14));
            service.TrialAutoConvert.Should().BeTrue();
            service.TrialGracePeriodDays.Should().Be(5);
            service.TrialPaymentDueAt.Should().BeNull();
        }
        finally
        {
            await SharedPostgresServer.DropDatabaseAsync(databaseName);
        }
    }

    /// <summary>The awaiting-payment status is stored as its name.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AwaitingPayment_IsStoredAsItsNameAsync()
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            await MigrateAsync(connectionString, PreviousMigration);
            var serviceId = await SeedTrialAsync(connectionString);
            await MigrateAsync(connectionString, TargetMigration);
            await using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = """UPDATE "ClientServices" SET "TrialStatus" = 'AwaitingPayment' WHERE "Id" = @id""";
                command.Parameters.AddWithValue("id", serviceId);
                await command.ExecuteNonQueryAsync();
            }

            await using var read = NewContext(connectionString);
            (await read.ClientServices.AsNoTracking().SingleAsync(s => s.Id == serviceId)).TrialStatus
                .Should().Be(TrialStatus.AwaitingPayment);
        }
        finally
        {
            await SharedPostgresServer.DropDatabaseAsync(databaseName);
        }
    }

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

    /// <summary>Runs a count query.</summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="sql">The query.</param>
    /// <returns>The count.</returns>
    private static async Task<long> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>Inserts an active trial row with foreign keys not enforced.</summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The new service's id.</returns>
    private static async Task<int> SeedTrialAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SET session_replication_role = replica;
            INSERT INTO "ClientServices" ("ClientId", "ProductId", "BillingCycle", "Status", "FirstPaymentAmount", "RecurringAmount",
                "CreatedAt", "AutoRenew", "AutoTerminateEndOfCycle", "HostingResourceRevision", "OverrideAutoSuspend", "Quantity", "SshKeyRevision",
                "TrialStatus", "TrialEndsAt", "TrialAutoConvert", "TrialGracePeriodDays")
            VALUES (1, 1, 'Monthly', 'Active', 0, 1, @now, true, false, 0, false, 1, 0, 'Active', @ends, true, 5) RETURNING "Id"
            """;
        command.Parameters.AddWithValue("now", TestMoments.Seeded);
        command.Parameters.AddWithValue("ends", TestMoments.Seeded.AddDays(14));
        return (int)(await command.ExecuteScalarAsync())!;
    }
}
