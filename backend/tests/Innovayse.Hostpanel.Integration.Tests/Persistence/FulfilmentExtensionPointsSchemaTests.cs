using System.Text.Json;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// The schema of the <c>FulfilmentExtensionPoints</c> migration against PostgreSQL: the product SKU,
/// metadata and grace period, the client's external account link, the product snapshot on an invoice
/// line, the invoice's lapse and digital flag, and the <c>FulfilmentRecords</c> table. Each test proves a
/// value round-trips or a constraint the database itself enforces, so a mapping that drifts from the
/// migration fails here rather than on a production write.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host, migrated to the latest migration.</param>
public sealed class FulfilmentExtensionPointsSchemaTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The fulfilment plugin id the records in these tests name.</summary>
    private const string PluginId = "test-licences";

    /// <summary>A product's SKU, metadata and a zero grace period are stored and read back as set.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Product_SkuMetadataAndZeroGracePeriod_RoundTripAsync()
    {
        var sku = UniqueSku();
        int productId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var product = await AddProductAsync(db);
            product.SetSku(sku.ToLowerInvariant());
            product.SetMetadata("""{ "tier": "pro", "seats": 5 }""");

            // Zero is the CLR default of the column; it must still be written, not replaced by the database default.
            product.SetGracePeriodDays(0);
            await db.SaveChangesAsync();
            productId = product.Id;
        }

        using var read = factory.Services.CreateScope();
        var loaded = await read.ServiceProvider.GetRequiredService<AppDbContext>().Products
            .AsNoTracking().SingleAsync(p => p.Id == productId);

        loaded.Sku.Should().Be(sku);
        loaded.GracePeriodDays.Should().Be(0);
        using var metadata = JsonDocument.Parse(loaded.Metadata!);
        metadata.RootElement.GetProperty("tier").GetString().Should().Be("pro");
        metadata.RootElement.GetProperty("seats").GetInt32().Should().Be(5);
    }

    /// <summary>A product saved without a grace period gets the default, and the column's database default matches it.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Product_WithoutGracePeriod_GetsTheDefaultAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await AddProductAsync(db);
        await db.SaveChangesAsync();

        var stored = await db.Database
            .SqlQuery<int>($"SELECT \"GracePeriodDays\" AS \"Value\" FROM \"Products\" WHERE \"Id\" = {product.Id}")
            .SingleAsync();

        stored.Should().Be(Product.DefaultGracePeriodDays);
        db.Model.FindEntityType(typeof(Product))!.FindProperty(nameof(Product.GracePeriodDays))!
            .GetDefaultValue().Should().Be(Product.DefaultGracePeriodDays, "rows that existed before the column get the same default");
    }

    /// <summary>A grace period outside the accepted range is refused by the check constraint, whoever writes it.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Product_GracePeriodOutOfRange_IsRefusedByTheDatabaseAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await AddProductAsync(db);
        await db.SaveChangesAsync();
        var tooLong = Product.MaxGracePeriodDays + 1;

        var act = () => db.Database.ExecuteSqlAsync(
            $"UPDATE \"Products\" SET \"GracePeriodDays\" = {tooLong} WHERE \"Id\" = {product.Id}");

        (await act.Should().ThrowAsync<PostgresException>())
            .Which.ConstraintName.Should().Be("CK_Products_GracePeriodDays");
    }

    /// <summary>Two products cannot share a SKU, while any number of products may have none.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Product_DuplicateSku_IsRefusedAndMissingSkusDoNotCollideAsync()
    {
        var sku = UniqueSku();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await AddProductAsync(db)).SetSku(sku);
            await AddProductAsync(db);
            await AddProductAsync(db);
            await db.SaveChangesAsync();
        }

        using var second = factory.Services.CreateScope();
        var secondDb = second.ServiceProvider.GetRequiredService<AppDbContext>();
        (await AddProductAsync(secondDb)).SetSku(sku);

        var act = () => secondDb.SaveChangesAsync();

        (await act.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<PostgresException>()
            .Which.ConstraintName.Should().Be("IX_Products_Sku");
    }

    /// <summary>A client's external account link round-trips, and one external account belongs to one client only.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_ExternalAccount_RoundTripsAndIsUniqueAsync()
    {
        var accountId = $"acct-{Guid.NewGuid():N}";
        int clientId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var client = NewClient();
            client.SetExternalAccount("crm", accountId);
            db.Clients.Add(client);
            await db.SaveChangesAsync();
            clientId = client.Id;
        }

        using var second = factory.Services.CreateScope();
        var secondDb = second.ServiceProvider.GetRequiredService<AppDbContext>();
        var loaded = await secondDb.Clients.AsNoTracking().SingleAsync(c => c.Id == clientId);
        loaded.ExternalAccountProvider.Should().Be("crm");
        loaded.ExternalAccountId.Should().Be(accountId);

        var other = NewClient();
        other.SetExternalAccount("crm", accountId);
        secondDb.Clients.Add(other);
        var act = () => secondDb.SaveChangesAsync();

        (await act.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<PostgresException>()
            .Which.ConstraintName.Should().Be("IX_Clients_ExternalAccountProvider_ExternalAccountId");
    }

    /// <summary>Half an external account link is refused by the check constraint, whoever writes it.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_HalfAnExternalAccount_IsRefusedByTheDatabaseAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = NewClient();
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var act = () => db.Database.ExecuteSqlAsync(
            $"UPDATE \"Clients\" SET \"ExternalAccountProvider\" = {"crm"} WHERE \"Id\" = {client.Id}");

        (await act.Should().ThrowAsync<PostgresException>())
            .Which.ConstraintName.Should().Be("CK_Clients_ExternalAccount");
    }

    /// <summary>
    /// A line's product snapshot round-trips in its own columns, a line without one reads back without
    /// one, and the invoice's digital flag and lapse time are stored.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Invoice_ProductSnapshotDigitalFlagAndLapse_RoundTripAsync()
    {
        var snapshot = new ProductSnapshot(42, BillingCycle.Annual, true, UniqueSku(), """{"seats":5}""");
        var lapsedAt = DateTimeOffset.UtcNow;
        int invoiceId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invoice = await AddInvoiceAsync(db);
            invoice.AddItem("Licence", 10m, 1, null, snapshot);
            invoice.AddItem("Setup fee", 5m, 1);
            invoice.MarkOverdue(lapsedAt);
            invoice.MarkLapsed(lapsedAt);
            await db.SaveChangesAsync();
            invoiceId = invoice.Id;
        }

        using var read = factory.Services.CreateScope();
        var readDb = read.ServiceProvider.GetRequiredService<AppDbContext>();
        var loaded = await readDb.Invoices.AsNoTracking().Include(i => i.Items).SingleAsync(i => i.Id == invoiceId);

        loaded.HasDigitalItems.Should().BeTrue();
        loaded.LapsedAt.Should().BeCloseTo(lapsedAt, TimeSpan.FromMilliseconds(1));
        var licence = loaded.Items.Single(i => i.Description == "Licence");
        licence.ProductSnapshot.Should().NotBeNull();
        licence.ProductSnapshot!.ProductId.Should().Be(snapshot.ProductId);
        licence.ProductSnapshot.BillingCycle.Should().Be(BillingCycle.Annual);
        licence.ProductSnapshot.IsDigital.Should().BeTrue();
        licence.ProductSnapshot.Sku.Should().Be(snapshot.Sku);
        using (var metadata = JsonDocument.Parse(licence.ProductSnapshot.Metadata!))
        {
            metadata.RootElement.GetProperty("seats").GetInt32().Should().Be(5);
        }

        loaded.Items.Single(i => i.Description == "Setup fee").ProductSnapshot.Should().BeNull();

        // The cycle is stored as its name, like every other billing cycle column.
        (await readDb.Database
            .SqlQuery<string>($"SELECT \"BillingCycle\" AS \"Value\" FROM \"InvoiceItems\" WHERE \"Id\" = {licence.Id}")
            .SingleAsync()).Should().Be(nameof(BillingCycle.Annual));
    }

    /// <summary>A fulfilment record round-trips with its kind and status stored as their names.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task FulfilmentRecord_RoundTripsAsync()
    {
        var now = DateTimeOffset.UtcNow;
        int recordId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invoice = await AddInvoiceAsync(db);
            await db.SaveChangesAsync();
            var record = FulfilmentRecord.Create(invoice.Id, PluginId, FulfilmentKind.Paid, now);
            db.FulfilmentRecords.Add(record);
            await db.SaveChangesAsync();
            recordId = record.Id;
        }

        using var read = factory.Services.CreateScope();
        var readDb = read.ServiceProvider.GetRequiredService<AppDbContext>();
        var loaded = await readDb.FulfilmentRecords.AsNoTracking().SingleAsync(r => r.Id == recordId);

        loaded.PluginId.Should().Be(PluginId);
        loaded.Kind.Should().Be(FulfilmentKind.Paid);
        loaded.Status.Should().Be(FulfilmentStatus.Pending);
        loaded.NextAttemptAt.Should().BeCloseTo(now, TimeSpan.FromMilliseconds(1));
        loaded.CreatedAt.Should().NotBe(default);
        (await readDb.Database
            .SqlQuery<string>($"SELECT \"Status\" AS \"Value\" FROM \"FulfilmentRecords\" WHERE \"Id\" = {recordId}")
            .SingleAsync()).Should().Be(nameof(FulfilmentStatus.Pending));
    }

    /// <summary>One plugin is told one thing about one invoice once: a second record for the same triple is refused.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task FulfilmentRecord_SameInvoicePluginAndKind_IsRefusedAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invoice = await AddInvoiceAsync(db);
        await db.SaveChangesAsync();
        db.FulfilmentRecords.Add(FulfilmentRecord.Create(invoice.Id, PluginId, FulfilmentKind.Paid, DateTimeOffset.UtcNow));
        db.FulfilmentRecords.Add(FulfilmentRecord.Create(invoice.Id, PluginId, FulfilmentKind.Refunded, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        db.FulfilmentRecords.Add(FulfilmentRecord.Create(invoice.Id, PluginId, FulfilmentKind.Paid, DateTimeOffset.UtcNow));
        var act = () => db.SaveChangesAsync();

        (await act.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<PostgresException>()
            .Which.ConstraintName.Should().Be("IX_FulfilmentRecords_InvoiceId_PluginId_Kind");
    }

    /// <summary>An invoice that has a fulfilment record cannot be deleted: the database refuses and the invoice stays.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Invoice_WithAFulfilmentRecord_CannotBeDeletedAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invoice = await AddInvoiceAsync(db);
        await db.SaveChangesAsync();
        db.FulfilmentRecords.Add(FulfilmentRecord.Create(invoice.Id, PluginId, FulfilmentKind.Paid, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        // The record is forgotten so the delete reaches the database, whose key is what is proven here;
        // with the record still tracked, the change tracker would refuse it first.
        db.DetachAll();
        db.Invoices.Remove(await db.Invoices.SingleAsync(i => i.Id == invoice.Id));
        var act = () => db.SaveChangesAsync();

        (await act.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<PostgresException>()
            .Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
    }

    /// <summary>A SKU no other test run uses, already in its normalised form.</summary>
    /// <returns>The SKU.</returns>
    private static string UniqueSku() => $"LIC-{Guid.NewGuid():N}".ToUpperInvariant();

    /// <summary>Builds an unsaved client with a unique subject and email.</summary>
    /// <returns>The client.</returns>
    private static Client NewClient() =>
        Client.Create($"subject-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);

    /// <summary>Saves a fresh product group and adds an unsaved digital product to it.</summary>
    /// <param name="db">The context the product is added to.</param>
    /// <returns>The tracked, unsaved product.</returns>
    private static async Task<Product> AddProductAsync(AppDbContext db)
    {
        var group = ProductGroup.Create($"fulfilment-{Guid.NewGuid():N}", null);
        db.ProductGroups.Add(group);
        await db.SaveChangesAsync();

        var product = Product.Create(group.Id, "Licence", null, null, null, null, ProductType.Digital, 0m, 0m, DateTimeOffset.UtcNow);
        db.Products.Add(product);
        return product;
    }

    /// <summary>Saves a fresh client and adds an unsaved unpaid invoice for them.</summary>
    /// <param name="db">The context the invoice is added to.</param>
    /// <returns>The tracked, unsaved invoice.</returns>
    private static async Task<Invoice> AddInvoiceAsync(AppDbContext db)
    {
        var client = NewClient();
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        var invoice = Invoice.Create(client.Id, DateTimeOffset.UtcNow.AddDays(-1), TestCurrencies.Base, DateTimeOffset.UtcNow);
        db.Invoices.Add(invoice);
        return invoice;
    }
}
