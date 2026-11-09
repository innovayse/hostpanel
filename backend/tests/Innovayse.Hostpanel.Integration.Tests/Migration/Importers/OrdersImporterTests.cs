using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Common;
using Innovayse.Hostpanel.Application.Migration.Importers;
using Innovayse.Hostpanel.Application.Migration.Pull;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Migration.Importers;

/// <summary>
/// <see cref="OrdersImporter"/>, driven end to end through <c>MigrationPullWorker.RunAsync</c>
/// against a <see cref="FakeMigrationPullSource"/>: imports an order for a resolved client,
/// deduplicated by order number, linking each service line to the product imported for its
/// source product id and leaving domain and addon lines out.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class OrdersImporterTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An order with no line items for a known client is imported.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RunAsync_Orders_ImportsAnOrderForAKnownClientAsync()
    {
        var source = new FakeMigrationPullSource { Totals = MigrationSourceTotals.Empty with { Billing = new MigrationBillingTotals(Orders: 1) } };
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var email = await MigrationTestSeeds.SeedClientAsync(scope.ServiceProvider, context);
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Orders);
        var orderNumber = $"ORD-{Guid.NewGuid().ToString("N")[..12]}"; // the column holds 20 characters
        source.SetDatasetJson("orders", $$"""
            [{"ClientEmail":"{{email}}","OrderNumber":"{{orderNumber}}","PaymentMethod":"manual","Status":"Active","Amount":10,"CreatedAt":"2025-01-01T00:00:00Z","Items":null}]
            """);

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var order = await context.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderNumber == orderNumber);
        Assert.NotNull(order);

        // Idempotent re-import: an order matched by its number is skipped the second time.
        var job2 = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Orders);
        await worker.RunAsync(job2.Id, CancellationToken.None);

        var orders = await context.Orders.AsNoTracking().Where(o => o.OrderNumber == orderNumber).ToListAsync();
        Assert.Single(orders);
    }

    /// <summary>
    /// A service line is linked to the product imported for its source product id, even when another
    /// product here carries the name the line was ordered under; the domain line beside it is not stored.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RunAsync_Orders_LinksAServiceLineToTheProductImportedForItsSourceIdAsync()
    {
        var source = new FakeMigrationPullSource { Totals = MigrationSourceTotals.Empty with { Billing = new MigrationBillingTotals(Orders: 1) } };
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var email = await MigrationTestSeeds.SeedClientAsync(scope.ServiceProvider, context);
        var product = await SeedProductAsync(context, $"product-{Guid.NewGuid():N}");
        var lineName = $"label-{Guid.NewGuid():N}";
        var decoy = await SeedProductAsync(context, lineName);
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Orders);
        var orderNumber = NewOrderNumber();
        source.SetDatasetJson("products", $$"""
            [{"Id":501,"GroupId":1,"Name":"{{product.Name}}","Description":null,"Type":"hostingaccount","MonthlyPrice":9.99,"AnnualPrice":null}]
            """);
        source.SetDatasetJson("orders", $$"""
            [{"ClientEmail":"{{email}}","OrderNumber":"{{orderNumber}}","PaymentMethod":"manual","Status":"Pending","Amount":20,"CreatedAt":"2025-01-01T00:00:00Z","Items":[
              {"ProductId":501,"ProductName":"{{lineName}}","BillingCycle":"Monthly","FirstPaymentAmount":9.99,"RecurringAmount":9.99,"Domain":"svc.test","Hostname":null},
              {"ProductId":null,"ProductName":"Domain registration","BillingCycle":"Annually","FirstPaymentAmount":10,"RecurringAmount":10,"Domain":"svc.test","Hostname":null}]}]
            """);

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var order = await context.Orders.AsNoTracking().Include(o => o.Items).SingleAsync(o => o.OrderNumber == orderNumber);
        var item = Assert.Single(order.Items);
        Assert.Equal(product.Id, item.ProductId);
        Assert.NotEqual(decoy.Id, item.ProductId);
        Assert.Equal(lineName, item.ProductName);
    }

    /// <summary>An order whose every line is a domain or an addon is skipped as having no product lines.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RunAsync_Orders_SkipsAnOrderWithOnlyDomainLinesAsync()
    {
        var source = new FakeMigrationPullSource { Totals = MigrationSourceTotals.Empty with { Billing = new MigrationBillingTotals(Orders: 1) } };
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var email = await MigrationTestSeeds.SeedClientAsync(scope.ServiceProvider, context);
        await SeedProductAsync(context, $"product-{Guid.NewGuid():N}");
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Orders);
        var orderNumber = NewOrderNumber();
        source.SetDatasetJson("orders", $$"""
            [{"ClientEmail":"{{email}}","OrderNumber":"{{orderNumber}}","PaymentMethod":"manual","Status":"Pending","Amount":10,"CreatedAt":"2025-01-01T00:00:00Z","Items":[
              {"ProductId":null,"ProductName":"Domain registration","BillingCycle":"Annually","FirstPaymentAmount":10,"RecurringAmount":10,"Domain":"only-domain.test","Hostname":null}]}]
            """);

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        Assert.False(await context.Orders.AsNoTracking().AnyAsync(o => o.OrderNumber == orderNumber));
        await AssertSkippedAsync(context, job.Id, MigrationSkipReason.NoProductLines);
    }

    /// <summary>An order with a service line whose source product was not imported here is skipped, never linked to another product.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RunAsync_Orders_SkipsAnOrderWhoseSourceProductIsUnknownAsync()
    {
        var source = new FakeMigrationPullSource { Totals = MigrationSourceTotals.Empty with { Billing = new MigrationBillingTotals(Orders: 1) } };
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var email = await MigrationTestSeeds.SeedClientAsync(scope.ServiceProvider, context);
        var product = await SeedProductAsync(context, $"product-{Guid.NewGuid():N}");
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Orders);
        var orderNumber = NewOrderNumber();
        source.SetDatasetJson("products", $$"""
            [{"Id":501,"GroupId":1,"Name":"{{product.Name}}","Description":null,"Type":"hostingaccount","MonthlyPrice":9.99,"AnnualPrice":null}]
            """);
        source.SetDatasetJson("orders", $$"""
            [{"ClientEmail":"{{email}}","OrderNumber":"{{orderNumber}}","PaymentMethod":"manual","Status":"Pending","Amount":10,"CreatedAt":"2025-01-01T00:00:00Z","Items":[
              {"ProductId":999,"ProductName":"{{product.Name}}","BillingCycle":"Monthly","FirstPaymentAmount":9.99,"RecurringAmount":9.99,"Domain":null,"Hostname":null}]}]
            """);

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        Assert.False(await context.Orders.AsNoTracking().AnyAsync(o => o.OrderNumber == orderNumber));
        await AssertSkippedAsync(context, job.Id, MigrationSkipReason.ProductNotFound);
    }

    /// <summary>A unique order number that fits the 20-character column.</summary>
    /// <returns>The order number.</returns>
    private static string NewOrderNumber() => $"ORD-{Guid.NewGuid().ToString("N")[..12]}";

    /// <summary>Seeds a product in a group of its own, as if an earlier products import had created it.</summary>
    /// <param name="context">The data context to seed into.</param>
    /// <param name="name">The product's name.</param>
    /// <returns>The saved product.</returns>
    private static async Task<Product> SeedProductAsync(IHostpanelDbContext context, string name)
    {
        var group = ProductGroup.Create($"group-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);
        var product = Product.Create(group.Id, name, null, null, null, null, ProductType.SharedHosting, 9.99m, 99.99m, DateTimeOffset.UtcNow);
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);
        return product;
    }

    /// <summary>Asserts the job logged exactly one order record, skipped for <paramref name="reason"/>.</summary>
    /// <param name="context">The data context to read the log from.</param>
    /// <param name="jobId">The job whose log is read.</param>
    /// <param name="reason">The skip reason the record must carry.</param>
    /// <returns>A task that completes once the log has been read.</returns>
    private static async Task AssertSkippedAsync(IHostpanelDbContext context, int jobId, MigrationSkipReason reason)
    {
        var log = await context.MigrationLogs.AsNoTracking()
            .SingleAsync(l => l.JobId == jobId && l.EntityType == MigrationEntityType.Orders);
        Assert.Equal(MigrationLogAction.Skipped, log.Action);
        Assert.Equal(reason, log.SkipReason);
    }
}
