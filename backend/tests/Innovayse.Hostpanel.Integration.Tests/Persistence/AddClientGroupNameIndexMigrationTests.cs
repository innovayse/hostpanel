using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// <c>AddClientGroupNameIndexAndInvoiceItemDiscount</c> on a database that already holds client groups sharing a name:
/// the migration tells them apart so the unique name index can be built, and keeps every group.
/// </summary>
public sealed class AddClientGroupNameIndexMigrationTests
{
    /// <summary>The migration before the one under test.</summary>
    private const string PreviousMigration = "20261111160128_AddEmailCampaignTrackingThrottleAndSegments";

    /// <summary>The migration under test.</summary>
    private const string MigrationUnderTest = "20261111190244_AddClientGroupNameIndexAndInvoiceItemDiscount";

    /// <summary>The width of the <c>ClientGroups.Name</c> column.</summary>
    private const int NameColumnLength = 100;

    /// <summary>
    /// Groups that share a name keep the name on the lowest id and are suffixed with their own id on the others, a name
    /// that is already as long as the column is cut to fit, no group is lost, and the unique index exists afterwards.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Up_GroupsSharingAName_AreToldApartAndNoneIsLostAsync()
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
            await MigrateAsync(options, PreviousMigration);
            var longName = new string('x', NameColumnLength);
            var first = await InsertGroupAsync(connectionString, "Wholesale");
            var second = await InsertGroupAsync(connectionString, "Wholesale");
            var third = await InsertGroupAsync(connectionString, "Wholesale");
            var unique = await InsertGroupAsync(connectionString, "Partners");
            var longFirst = await InsertGroupAsync(connectionString, longName);
            var longSecond = await InsertGroupAsync(connectionString, longName);

            await MigrateAsync(options, MigrationUnderTest);

            (await NameOfAsync(connectionString, first)).Should().Be("Wholesale", "the lowest id keeps the name");
            (await NameOfAsync(connectionString, unique)).Should().Be("Partners", "a name nobody shares stays");
            (await NameOfAsync(connectionString, longFirst)).Should().Be(longName);
            var longRenamed = await NameOfAsync(connectionString, longSecond);
            longRenamed.Should().HaveLength(NameColumnLength).And.EndWith($" ({longSecond})");
            (await NameOfAsync(connectionString, third)).Should().Be($"Wholesale ({third})");
            (await NameOfAsync(connectionString, second)).Should().Be($"Wholesale ({second})");
            var groups = await ScalarAsync(connectionString, """SELECT count(*) FROM "ClientGroups" """);
            groups.Should().Be(6L, "no group is lost");
            var names = await ScalarAsync(connectionString, """SELECT count(DISTINCT "Name") FROM "ClientGroups" """);
            names.Should().Be(6L, "every name is now different");
            var index = await ScalarAsync(
                connectionString, "SELECT count(*) FROM pg_indexes WHERE indexname = 'IX_ClientGroups_Name'");
            index.Should().Be(1L);
        }
        finally
        {
            using var probe = new NpgsqlConnection(connectionString);
            NpgsqlConnection.ClearPool(probe);
            await SharedPostgresServer.DropDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// A group already literally named after the suffixed form of a duplicate ("Name (Id)" for the same Id) keeps its
    /// name, the duplicate gets a different suffix, and the unique index can still be built.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Up_AGroupAlreadyNamedLikeTheSuffixedDuplicate_IsKeptAndTheDuplicateGetsAnotherSuffixAsync()
    {
        var databaseName = $"it_{Guid.NewGuid():N}";
        var connectionString = await SharedPostgresServer.CreateDatabaseAsync(databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).Options;
            await MigrateAsync(options, PreviousMigration);
            var first = await InsertGroupAsync(connectionString, "Wholesale");
            var second = await InsertGroupAsync(connectionString, "Wholesale");
            var taken = await InsertGroupAsync(connectionString, $"Wholesale ({second})");

            await MigrateAsync(options, MigrationUnderTest);

            (await NameOfAsync(connectionString, first)).Should().Be("Wholesale");
            (await NameOfAsync(connectionString, taken)).Should().Be($"Wholesale ({second})", "an existing name is not taken over");
            (await NameOfAsync(connectionString, second)).Should().Be($"Wholesale ({second}-2)");
            var names = await ScalarAsync(connectionString, """SELECT count(DISTINCT "Name") FROM "ClientGroups" """);
            names.Should().Be(3L, "every name is different");
            var index = await ScalarAsync(
                connectionString, "SELECT count(*) FROM pg_indexes WHERE indexname = 'IX_ClientGroups_Name'");
            index.Should().Be(1L);
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

    /// <summary>
    /// Inserts a client group under a name, naming only the columns the schema had before the migration.
    /// </summary>
    /// <param name="connectionString">The connection string of the test database.</param>
    /// <param name="name">The group's name.</param>
    /// <returns>The group's id.</returns>
    private static async Task<int> InsertGroupAsync(string connectionString, string name)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO "ClientGroups" ("Name", "DiscountPercent", "Color", "IsDefault")
            VALUES (@name, 10, '#3B82F6', false) RETURNING "Id"
            """;
        command.Parameters.AddWithValue("name", name);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>Reads the name of one group.</summary>
    /// <param name="connectionString">The connection string of the test database.</param>
    /// <param name="id">The group's id.</param>
    /// <returns>The group's name.</returns>
    private static async Task<string> NameOfAsync(string connectionString, int id)
    {
        var sql = $"SELECT \"Name\" FROM \"ClientGroups\" WHERE \"Id\" = {id}";
        return (string)(await ScalarAsync(connectionString, sql))!;
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
