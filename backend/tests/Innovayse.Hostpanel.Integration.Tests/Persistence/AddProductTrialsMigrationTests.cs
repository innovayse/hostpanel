using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// <c>AddProductTrials</c> on a database that already holds products: each keeps the trial off, takes the defaults of the
/// other trial settings and, like a new product, turns a trial into a paid service by default.
/// </summary>
public sealed class AddProductTrialsMigrationTests
{
    /// <summary>The migration before the one under test: the schema every database has before the trial settings exist.</summary>
    private const string PreviousMigration = "20261116151510_AddProductOrderLimits";

    /// <summary>The migration under test.</summary>
    private const string TargetMigration = "20261117083614_AddProductTrials";

    /// <summary>A product stored before the migration loads with the trial off and the default settings.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Up_OnExistingProducts_KeepsTheTrialOffWithTheDefaultsAsync()
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

            var productId = await SeedProductAsync(connectionString);

            await using (var context = NewContext(options))
            {
                await context.GetService<IMigrator>().MigrateAsync(TargetMigration);
            }

            await using var read = NewContext(options);
            var product = await read.Products.AsNoTracking().SingleAsync(p => p.Id == productId);

            product.Trial.Enabled.Should().BeFalse("no product offered a trial before the setting existed");
            product.Trial.Days.Should().Be(14);
            product.Trial.AutoConvert.Should().BeTrue("a trial converts to a paid service by default, as on a new product");
            product.Trial.ReminderDays.Should().Equal(3, 1);
            product.Trial.GracePeriodDays.Should().Be(3);
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

    /// <summary>Inserts a group and a product in it, naming only columns the schema before the migration has.</summary>
    /// <param name="connectionString">The connection string of the database.</param>
    /// <returns>The new product's id.</returns>
    private static async Task<int> SeedProductAsync(string connectionString)
    {
        var groupId = await InsertAsync(
            connectionString,
            """
            INSERT INTO "ProductGroups" ("Name", "IsActive") VALUES ('Existing', true) RETURNING "Id"
            """);

        return await InsertAsync(
            connectionString,
            """
            INSERT INTO "Products" ("GroupId", "Name", "Type", "Status", "MonthlyPrice", "AnnualPrice", "IsFeatured", "SortOrder", "CreatedAt")
            VALUES (@group, 'Existing plan', 'SharedHosting', 'Active', 1, 10, false, 0, @now) RETURNING "Id"
            """,
            ("group", groupId),
            ("now", TestMoments.Seeded));
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
