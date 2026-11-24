using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// <c>AddServiceWebsitePhpCliPath</c> on a database that already holds websites a plugin set up: Up keeps every row and
/// value and leaves the new <c>PhpCliPath</c> column empty, so the host learns it on the next ensure; Down keeps every row
/// and drops only that column; Up again restores the column, empty.
/// </summary>
public sealed class AddServiceWebsitePhpCliPathMigrationTests
{
    /// <summary>The migration before the one under test.</summary>
    private const string PreviousMigration = "20261123200258_AddSshKeyAllowedFrom";

    /// <summary>Applying, reverting and reapplying the migration over existing websites keeps every row and value.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UpDownUp_OnExistingWebsites_KeepsEveryRowAndValueAsync()
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

            await SeedExistingWebsitesAsync(connectionString);
            var before = await SnapshotAsync(connectionString);
            before.Should().HaveCount(2);

            await using (var context = NewContext(options))
            {
                await context.Database.MigrateAsync();
            }

            (await SnapshotAsync(connectionString)).Should().BeEquivalentTo(before, "Up adds a column and copies nothing");
            (await ScalarAsync(connectionString, "SELECT count(*) FROM \"ServiceWebsites\" WHERE \"PhpCliPath\" IS NULL")).Should().Be(2L);

            // The upgraded model reads the old rows: a website that runs PHP waits for its CLI path, one without PHP does not.
            await using (var context = NewContext(options))
            {
                var websites = await context.ServiceWebsites.AsNoTracking().OrderBy(w => w.Hostname).ToListAsync();
                websites.Select(w => w.PhpCliPath).Should().OnlyContain(path => path == null);
                websites.Select(w => w.NeedsPhpCliPath).Should().Equal(true, false);
            }

            await using (var context = NewContext(options))
            {
                await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            }

            (await SnapshotAsync(connectionString)).Should().BeEquivalentTo(before, "Down drops only the new column");
            (await ScalarAsync(connectionString, "SELECT count(*) FROM information_schema.columns WHERE table_name = 'ServiceWebsites' AND column_name = 'PhpCliPath'")).Should().Be(0L);

            await using (var context = NewContext(options))
            {
                await context.Database.MigrateAsync();
            }

            (await SnapshotAsync(connectionString)).Should().BeEquivalentTo(before, "Up again copies nothing either");
            (await ScalarAsync(connectionString, "SELECT count(*) FROM \"ServiceWebsites\" WHERE \"PhpCliPath\" IS NULL")).Should().Be(2L);
        }
        finally
        {
            using var probe = new NpgsqlConnection(connectionString);
            NpgsqlConnection.ClearPool(probe);
            await SharedPostgresServer.DropDatabaseAsync(databaseName);
        }
    }

    /// <summary>Opens a context over the test database; nothing here reads an encrypted column.</summary>
    /// <param name="options">The options pointing at the test database.</param>
    /// <returns>A new context.</returns>
    private static AppDbContext NewContext(DbContextOptions<AppDbContext> options) =>
        new(options, new ModelOnlyEncryptionService());

    /// <summary>
    /// A client, a product, an active service and two active websites of it: one that runs PHP and one that does not.
    /// </summary>
    /// <remarks>
    /// The rows are written in SQL naming only the columns the schema had at <see cref="PreviousMigration"/>.
    /// Saving entities would write every column the model has today, and the column under test does not exist yet in
    /// this older schema.
    /// </remarks>
    /// <param name="connectionString">The connection string of the test database.</param>
    /// <returns>A task that completes once the rows are written.</returns>
    private static async Task SeedExistingWebsitesAsync(string connectionString)
    {
        var now = TestMoments.Seeded;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var clientId = await InsertAsync(
            connection,
            """
            INSERT INTO "Clients" ("UserId", "FirstName", "LastName", "Status", "CreatedAt",
                "AllowSso", "DisableCcProcessing", "LateFees", "MarketingOptIn", "NotifyAffiliate", "NotifyDomain",
                "NotifyGeneral", "NotifyInvoice", "NotifyProduct", "NotifySupport", "OverdueNotices",
                "SeparateInvoices", "StatusUpdate", "TaxExempt")
            VALUES (@user, 'Ada', 'Lovelace', 'Active', @now,
                false, false, false, false, false, false, false, false, false, false, false, false, false, false)
            RETURNING "Id"
            """,
            ("user", $"subject-{Guid.NewGuid():N}"),
            ("now", now));

        var groupId = await InsertAsync(
            connection,
            """INSERT INTO "ProductGroups" ("Name", "IsActive") VALUES (@name, true) RETURNING "Id" """,
            ("name", $"mig-{Guid.NewGuid():N}"));

        var productId = await InsertAsync(
            connection,
            """
            INSERT INTO "Products" ("GroupId", "Name", "Type", "Status", "MonthlyPrice", "AnnualPrice", "IsFeatured", "SortOrder", "CreatedAt")
            VALUES (@group, 'Starter', 'SharedHosting', 'Active', 0, 0, false, 0, @now) RETURNING "Id"
            """,
            ("group", groupId),
            ("now", now));

        var serviceId = await InsertAsync(
            connection,
            """
            INSERT INTO "ClientServices" ("ClientId", "ProductId", "Username", "BillingCycle", "Status", "Quantity",
                "FirstPaymentAmount", "RecurringAmount", "AutoRenew", "AutoTerminateEndOfCycle", "OverrideAutoSuspend", "CreatedAt")
            VALUES (@client, @product, 'web', 'monthly', 'Active', 1, 0, 0, true, false, false, @now) RETURNING "Id"
            """,
            ("client", clientId),
            ("product", productId),
            ("now", now));

        foreach (var (hostname, php) in new[] { ("app.example.test", "8.3"), ("plain.example.test", (string?)null) })
        {
            await InsertAsync(
                connection,
                """
                INSERT INTO "ServiceWebsites" ("ClientServiceId", "ServerId", "AccountUsername", "IssuedToPluginId", "State", "WebsiteId",
                    "Hostname", "DocumentRoot", "PhpVersion", "DocumentRootMayBeLink")
                VALUES (@service, 1, 'web', 'acme.sites', 'Active', @website, @hostname, 'public_html', @php, false) RETURNING "Id"
                """,
                ("service", serviceId),
                ("website", Guid.NewGuid()),
                ("hostname", hostname),
                ("php", (object?)php ?? DBNull.Value));
        }
    }

    /// <summary>Runs one insert that returns the new row's id.</summary>
    /// <param name="connection">The open connection to the test database.</param>
    /// <param name="sql">The insert, ending in <c>RETURNING "Id"</c>.</param>
    /// <param name="parameters">The named parameters of the insert.</param>
    /// <returns>The new row's id.</returns>
    private static async Task<int> InsertAsync(NpgsqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (int)(await command.ExecuteScalarAsync())!;
    }

    /// <summary>Every pre-existing value of the website rows, as text, for an exact before/after comparison.</summary>
    /// <param name="connectionString">The connection string of the test database.</param>
    /// <returns>One line per row, the values joined by a bar.</returns>
    private static async Task<List<string>> SnapshotAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var rows = new List<string>();
        await using var command = new NpgsqlCommand(
            "SELECT \"ClientServiceId\",\"ServerId\",\"AccountUsername\",\"IssuedToPluginId\",\"State\",\"WebsiteId\",\"Hostname\",\"DocumentRoot\",\"PhpVersion\",\"DocumentRootMayBeLink\" FROM \"ServiceWebsites\" ORDER BY \"Hostname\"",
            connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(string.Join("|", Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "<null>" : Convert.ToString(reader.GetValue(i)))));
        }

        return rows;
    }

    /// <summary>Runs a scalar query.</summary>
    /// <param name="connectionString">The connection string of the test database.</param>
    /// <param name="sql">The query.</param>
    /// <returns>The first column of the first row.</returns>
    private static async Task<object?> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync();
    }
}
