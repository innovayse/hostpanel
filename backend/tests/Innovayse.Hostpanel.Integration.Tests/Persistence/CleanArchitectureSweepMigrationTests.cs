using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// The billing cycle step of <c>CleanArchitectureSweep</c>: a legacy 'Free Account' cycle on a row that recurs
/// at zero becomes the monthly cycle, while any other unknown cycle, a priced 'Free Account' included, stops
/// the migration with a message that names the value.
/// </summary>
public sealed class CleanArchitectureSweepMigrationTests
{
    /// <summary>The migration before the one under test.</summary>
    private const string PreviousMigration = "20261027103133_AddSshAgentIdentityAndBinding";

    /// <summary>The migration under test.</summary>
    private const string SweepMigration = "20261028101742_CleanArchitectureSweep";

    /// <summary>Free services and order items that recur at zero end on the monthly cycle.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Up_FreeAccountRowsAtZero_BecomeMonthlyAsync()
    {
        await RunOnPreviousSchemaAsync(async (options, connectionString) =>
        {
            var (clientId, productId) = await SeedClientAndProductAsync(connectionString);
            var serviceId = await InsertServiceAsync(connectionString, clientId, productId, "Free Account", 0m);
            var itemId = await InsertOrderItemAsync(connectionString, clientId, productId, " free account ", 0m);

            await using (var context = NewContext(options))
            {
                await context.GetService<IMigrator>().MigrateAsync(SweepMigration);
            }

            (await ScalarAsync(connectionString, $"SELECT \"BillingCycle\" FROM \"ClientServices\" WHERE \"Id\" = {serviceId}")).Should().Be("Monthly");
            (await ScalarAsync(connectionString, $"SELECT \"BillingCycle\" FROM \"OrderItems\" WHERE \"Id\" = {itemId}")).Should().Be("Monthly");
        });
    }

    /// <summary>A cycle the model has no member for stops the migration and the message names it.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Up_QuarterlyCycle_StopsWithTheValueNamedAsync()
    {
        await RunOnPreviousSchemaAsync(async (options, connectionString) =>
        {
            var (clientId, productId) = await SeedClientAndProductAsync(connectionString);
            await InsertServiceAsync(connectionString, clientId, productId, "Quarterly", 10m);

            await AssertSweepStopsAsync(options, connectionString, "'Quarterly'");
        });
    }

    /// <summary>A 'Free Account' row that is charged is not free, so it stops the migration instead of becoming monthly.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Up_PricedFreeAccount_StopsWithTheValueNamedAsync()
    {
        await RunOnPreviousSchemaAsync(async (options, connectionString) =>
        {
            var (clientId, productId) = await SeedClientAndProductAsync(connectionString);
            var serviceId = await InsertServiceAsync(connectionString, clientId, productId, "Free Account", 5m);

            await AssertSweepStopsAsync(options, connectionString, "'Free Account'");

            (await ScalarAsync(connectionString, $"SELECT \"BillingCycle\" FROM \"ClientServices\" WHERE \"Id\" = {serviceId}")).Should().Be("Free Account");
        });
    }

    /// <summary>Creates a throwaway database at <see cref="PreviousMigration"/>, runs the body and drops the database.</summary>
    /// <param name="body">The test body, given the context options and the connection string.</param>
    /// <returns>A task that completes when the body has run and the database is dropped.</returns>
    private static async Task RunOnPreviousSchemaAsync(Func<DbContextOptions<AppDbContext>, string, Task> body)
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

            await body(options, connectionString);
        }
        finally
        {
            using var probe = new NpgsqlConnection(connectionString);
            NpgsqlConnection.ClearPool(probe);
            await SharedPostgresServer.DropDatabaseAsync(databaseName);
        }
    }

    /// <summary>
    /// Runs the sweep and asserts it fails with a message naming <paramref name="expectedValue"/>, and that the
    /// migration was not recorded as applied.
    /// </summary>
    /// <param name="options">The options pointing at the test database.</param>
    /// <param name="connectionString">The connection string of the test database.</param>
    /// <param name="expectedValue">The quoted cycle the message must name.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    private static async Task AssertSweepStopsAsync(DbContextOptions<AppDbContext> options, string connectionString, string expectedValue)
    {
        await using (var context = NewContext(options))
        {
            var migrate = () => context.GetService<IMigrator>().MigrateAsync(SweepMigration);
            await migrate.Should().ThrowAsync<PostgresException>()
                .Where(e => e.MessageText.Contains(expectedValue, StringComparison.Ordinal));
        }

        (await ScalarAsync(connectionString, $"SELECT count(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '{SweepMigration}'"))
            .Should().Be(0L, "the failed sweep rolls back as a whole");
    }

    /// <summary>Opens a context over the test database; nothing here reads an encrypted column.</summary>
    /// <param name="options">The options pointing at the test database.</param>
    /// <returns>A new context.</returns>
    private static AppDbContext NewContext(DbContextOptions<AppDbContext> options) =>
        new(options, new ModelOnlyEncryptionService());

    /// <summary>Inserts a client and a free product in its own group.</summary>
    /// <remarks>
    /// The rows are written in SQL naming only the columns the schema had at <see cref="PreviousMigration"/>.
    /// Saving entities would write every column the model has today, and a column a later migration adds
    /// does not exist yet in this older schema.
    /// </remarks>
    /// <param name="connectionString">The connection string of the test database.</param>
    /// <returns>The ids of the client and the product.</returns>
    private static async Task<(int ClientId, int ProductId)> SeedClientAndProductAsync(string connectionString)
    {
        var now = TestMoments.Seeded;
        var clientId = await InsertAsync(
            connectionString,
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
            connectionString,
            """INSERT INTO "ProductGroups" ("Name", "IsActive") VALUES (@name, true) RETURNING "Id" """,
            ("name", $"mig-{Guid.NewGuid():N}"));

        var productId = await InsertAsync(
            connectionString,
            """
            INSERT INTO "Products" ("GroupId", "Name", "Type", "Status", "MonthlyPrice", "AnnualPrice", "IsFeatured", "SortOrder", "CreatedAt")
            VALUES (@group, 'Free Hosting', 'SharedHosting', 'Active', 0, 0, false, 0, @now) RETURNING "Id"
            """,
            ("group", groupId),
            ("now", now));

        return (clientId, productId);
    }

    /// <summary>Inserts an active service on the given cycle and recurring amount.</summary>
    /// <param name="connectionString">The connection string of the test database.</param>
    /// <param name="clientId">The client the service belongs to.</param>
    /// <param name="productId">The product the service is of.</param>
    /// <param name="billingCycle">The billing cycle as the legacy column holds it.</param>
    /// <param name="recurringAmount">The amount the service recurs at.</param>
    /// <returns>The new service's id.</returns>
    private static Task<int> InsertServiceAsync(string connectionString, int clientId, int productId, string billingCycle, decimal recurringAmount) =>
        InsertAsync(
            connectionString,
            """
            INSERT INTO "ClientServices" ("ClientId", "ProductId", "Username", "BillingCycle", "Status", "Quantity",
                "FirstPaymentAmount", "RecurringAmount", "AutoRenew", "AutoTerminateEndOfCycle", "OverrideAutoSuspend", "CreatedAt")
            VALUES (@client, @product, 'web', @cycle, 'Active', 1, @amount, @amount, true, false, false, @now) RETURNING "Id"
            """,
            ("client", clientId),
            ("product", productId),
            ("cycle", billingCycle),
            ("amount", recurringAmount),
            ("now", TestMoments.Seeded));

    /// <summary>Inserts a pending order with one pending item on the given cycle and recurring amount.</summary>
    /// <param name="connectionString">The connection string of the test database.</param>
    /// <param name="clientId">The client who placed the order.</param>
    /// <param name="productId">The product the item is of.</param>
    /// <param name="billingCycle">The billing cycle as the legacy column holds it.</param>
    /// <param name="recurringAmount">The amount the item recurs at.</param>
    /// <returns>The new order item's id.</returns>
    private static async Task<int> InsertOrderItemAsync(string connectionString, int clientId, int productId, string billingCycle, decimal recurringAmount)
    {
        var orderId = await InsertAsync(
            connectionString,
            """
            INSERT INTO "Orders" ("OrderNumber", "ClientId", "Status", "PaymentMethod", "CreatedAt", "PaymentToken", "Currency")
            VALUES (@number, @client, 'Pending', 'Manual', @now, @token, 'USD') RETURNING "Id"
            """,
            ("number", $"FA-{Guid.NewGuid():N}"[..20]),
            ("client", clientId),
            ("now", TestMoments.Seeded),
            ("token", Guid.NewGuid().ToString("N")));

        return await InsertAsync(
            connectionString,
            """
            INSERT INTO "OrderItems" ("OrderId", "ProductId", "ProductName", "BillingCycle", "Status", "FirstPaymentAmount", "RecurringAmount")
            VALUES (@order, @product, 'Free Hosting', @cycle, 'Pending', @amount, @amount) RETURNING "Id"
            """,
            ("order", orderId),
            ("product", productId),
            ("cycle", billingCycle),
            ("amount", recurringAmount));
    }

    /// <summary>Runs one insert that returns the new row's id.</summary>
    /// <param name="connectionString">The connection string of the test database.</param>
    /// <param name="sql">The insert, ending in <c>RETURNING "Id"</c>.</param>
    /// <param name="parameters">The named parameters of the insert.</param>
    /// <returns>The new row's id.</returns>
    private static async Task<int> InsertAsync(string connectionString, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (int)(await command.ExecuteScalarAsync())!;
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
