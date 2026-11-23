using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// <c>AddSshAgentIdentityAndBinding</c> on a database that already holds servers, SSH settings and
/// SSH keys (the state production is in after the first SSH release): Up keeps every row and value
/// and leaves the two new columns empty; Down keeps every row and drops only those two columns;
/// Up again restores the columns, empty.
/// </summary>
public sealed class AddSshAgentIdentityAndBindingMigrationTests
{
    /// <summary>The migration before the one under test.</summary>
    private const string PreviousMigration = "20261027064149_AddSshKeyManagement";

    /// <summary>Applying, reverting and reapplying the migration over existing SSH data keeps every row and value.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UpDownUp_OnExistingSshData_KeepsEveryRowAndValueAsync()
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

            var (serverId, serviceId) = await SeedExistingStateAsync(connectionString);
            var before = await SnapshotAsync(connectionString);

            await using (var context = NewContext(options))
            {
                await context.Database.MigrateAsync();
            }

            (await SnapshotAsync(connectionString)).Should().BeEquivalentTo(before, "Up adds columns and copies nothing");
            (await ScalarAsync(connectionString, "SELECT \"AgentBinding\" IS NULL AND \"AgentPublicKey\" IS NULL FROM \"ServerSshSettings\"")).Should().Be(true);

            // The upgraded model reads the old rows: an existing key keeps working, unbound.
            await using (var context = NewContext(options))
            {
                // Projected, so the encrypted key column is not decrypted (it holds a placeholder here).
                var settings = await context.ServerSshSettings.AsNoTracking()
                    .Where(s => s.ServerId == serverId)
                    .Select(s => new { s.AgentBinding, s.IsEnabled })
                    .SingleAsync();
                settings.AgentBinding.Should().BeNull();
                settings.IsEnabled.Should().BeTrue();
                (await context.SshKeys.AsNoTracking().CountAsync(k => k.ClientServiceId == serviceId)).Should().Be(1);
            }

            await using (var context = NewContext(options))
            {
                await context.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            }

            (await SnapshotAsync(connectionString)).Should().BeEquivalentTo(before, "Down drops only the two new columns");
            (await ScalarAsync(connectionString, "SELECT count(*) FROM information_schema.columns WHERE table_name = 'ServerSshSettings' AND column_name IN ('AgentBinding','AgentPublicKey')")).Should().Be(0L);

            await using (var context = NewContext(options))
            {
                await context.Database.MigrateAsync();
            }

            (await SnapshotAsync(connectionString)).Should().BeEquivalentTo(before, "Up again copies nothing either");
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
    /// A client, a product, a server, an active service on that server with one installed SSH key, and the
    /// server's SSH settings.
    /// </summary>
    /// <remarks>
    /// The rows are written in SQL naming only the columns the schema had at <see cref="PreviousMigration"/>.
    /// Saving entities would write every column the model has today, and a column a later migration adds
    /// does not exist yet in this older schema.
    /// </remarks>
    /// <param name="connectionString">The connection string of the test database.</param>
    /// <returns>The ids of the seeded server and service.</returns>
    private static async Task<(int ServerId, int ServiceId)> SeedExistingStateAsync(string connectionString)
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

        var serverId = await InsertAsync(
            connection,
            """
            INSERT INTO "Servers" ("Name", "Hostname", "Module", "Username", "UseSSL", "IsDefault", "IsDisabled", "MonthlyCost", "CreatedAt")
            VALUES ('existing', 'existing.example.test', 'examplepanel7', 'root', true, false, false, 0, @now) RETURNING "Id"
            """,
            ("now", now));

        var serviceId = await InsertAsync(
            connection,
            """
            INSERT INTO "ClientServices" ("ClientId", "ProductId", "ServerId", "Username", "BillingCycle", "Status", "Quantity",
                "FirstPaymentAmount", "RecurringAmount", "AutoRenew", "AutoTerminateEndOfCycle", "OverrideAutoSuspend", "CreatedAt")
            VALUES (@client, @product, @server, 'web', 'monthly', 'Active', 1, 0, 0, true, false, false, @now) RETURNING "Id"
            """,
            ("client", clientId),
            ("product", productId),
            ("server", serverId),
            ("now", now));

        await InsertAsync(
            connection,
            """
            INSERT INTO "SshKeys" ("KeyId", "ClientServiceId", "ServerId", "AccountUsername", "Label", "PublicKey", "Fingerprint", "SyncState", "CreatedAt")
            VALUES (@key, @service, @server, 'web', 'laptop',
                'ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIG6EJIHRxsxge2aZnl+clPBfoX0nmvD7FfHXM2QQ3+3b',
                'SHA256:xK0G2nacQ5l2jswa38Dl9jmRCsESwmesIiz26XjE5+o', 'Installed', @now) RETURNING "Id"
            """,
            ("key", Guid.NewGuid()),
            ("service", serviceId),
            ("server", serverId),
            ("now", now));

        await InsertAsync(
            connection,
            """
            INSERT INTO "ServerSshSettings" ("ServerId", "IsEnabled", "SshHostname", "SshPort", "AgentHost", "AgentPort", "AgentUsername",
                "AgentPrivateKey", "AgentHostKeyFingerprint", "ConnectTimeoutSeconds", "OperationTimeoutSeconds", "IsHealthy")
            VALUES (@server, true, 'existing.example.test', 2299, '10.0.0.9', 22, 'hpsshkeys', 'ENCRYPTED-BLOB',
                'SHA256:V8WRk2pDzj4fSTJHYxEiHG4otIDHJcABkgUk2/Swhwk', 10, 20, true) RETURNING "Id"
            """,
            ("server", serverId));

        return (serverId, serviceId);
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

    /// <summary>Every pre-existing value of the SSH tables, as text, for an exact before/after comparison.</summary>
    /// <param name="connectionString">The connection string of the test database.</param>
    /// <returns>One line per row, the values joined by a bar.</returns>
    private static async Task<List<string>> SnapshotAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var rows = new List<string>();
        foreach (var sql in new[]
        {
            "SELECT \"ServerId\",\"IsEnabled\",\"SshHostname\",\"SshPort\",\"AgentHost\",\"AgentPort\",\"AgentUsername\",\"AgentPrivateKey\",\"AgentHostKeyFingerprint\",\"ConnectTimeoutSeconds\",\"OperationTimeoutSeconds\",\"IsHealthy\" FROM \"ServerSshSettings\" ORDER BY \"Id\"",
            "SELECT \"KeyId\",\"ClientServiceId\",\"ServerId\",\"AccountUsername\",\"Label\",\"PublicKey\",\"Fingerprint\",\"SyncState\" FROM \"SshKeys\" ORDER BY \"Id\"",
            "SELECT count(*) FROM \"Servers\"",
        })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(string.Join("|", Enumerable.Range(0, reader.FieldCount).Select(i => reader.IsDBNull(i) ? "<null>" : Convert.ToString(reader.GetValue(i)))));
            }
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
