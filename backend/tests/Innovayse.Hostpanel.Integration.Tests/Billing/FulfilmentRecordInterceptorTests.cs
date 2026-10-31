using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="FulfilmentRecordInterceptor"/> against Postgres: a digital invoice that is paid, refunded or
/// lapsed gets one record per enabled plugin in the same save, a failed save leaves none, a repeated event
/// writes no duplicate, no enabled plugin means no record, and a save that discards domain events writes none.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class FulfilmentRecordInterceptorTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The reference the invoices are paid under.</summary>
    private const string Reference = "interceptor-ref";

    /// <summary>Paying a digital invoice writes the paid record of every enabled plugin in the payment's own save.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Paying_WritesOneRecordPerEnabledPlugin()
    {
        var first = $"stub-{Guid.NewGuid():N}";
        var second = $"stub-{Guid.NewGuid():N}";
        using var host = HostWith(first, second);
        var invoiceId = await SeedUnpaidAsync(host, digital: true);

        await MutateAsync(host, invoiceId, i => i.Settle("manual", Reference, DateTimeOffset.UtcNow, TransactionSource.Manual));

        var records = await RecordsAsync(host, invoiceId);
        records.Select(r => (r.PluginId, r.Kind)).Should().BeEquivalentTo(
            [(first, FulfilmentKind.Paid), (second, FulfilmentKind.Paid)]);
    }

    /// <summary>A refund and a lapse write their own kinds, and a second part refund does not write another.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task RefundingAndLapsing_WriteTheirOwnKindOnce()
    {
        var plugin = $"stub-{Guid.NewGuid():N}";
        using var host = HostWith(plugin);
        var invoiceId = await SeedUnpaidAsync(host, digital: true);
        var now = DateTimeOffset.UtcNow;
        await MutateAsync(host, invoiceId, i => i.MarkOverdue(now));
        await MutateAsync(host, invoiceId, i => i.MarkLapsed(now));
        await MutateAsync(host, invoiceId, i => i.Settle("manual", Reference, now, TransactionSource.Manual));

        await MutateAsync(host, invoiceId, i => i.AddRefund(now, "manual", "r-1", 1m, 0m, null, false, TransactionSource.Manual));
        await MutateAsync(host, invoiceId, i => i.AddRefund(now, "manual", "r-2", 1m, 0m, null, false, TransactionSource.Manual));

        var records = await RecordsAsync(host, invoiceId);
        records.Select(r => r.Kind).Should().BeEquivalentTo(
            [FulfilmentKind.Lapsed, FulfilmentKind.Paid, FulfilmentKind.Refunded]);
    }

    /// <summary>A payment whose save fails commits no record: the records live and die with the payment.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task FailedPaymentSave_LeavesNoRecord()
    {
        var plugin = $"stub-{Guid.NewGuid():N}";
        using var host = HostWith(plugin);
        var invoiceId = await SeedUnpaidAsync(host, digital: true);
        using var loser = host.Services.CreateScope();
        var loserDb = loser.ServiceProvider.GetRequiredService<AppDbContext>();
        var stale = await loserDb.Invoices.Include(i => i.Items).Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId);
        await MutateAsync(host, invoiceId, i => i.UpdateNotes("changed first"));

        stale.Settle("manual", Reference, DateTimeOffset.UtcNow, TransactionSource.Manual);
        var save = () => loserDb.SaveChangesAsync(CancellationToken.None);

        await save.Should().ThrowAsync<ConcurrencyConflictException>();
        (await RecordsAsync(host, invoiceId)).Should().BeEmpty();
    }

    /// <summary>A record that already exists is not written again, so a repeated payment event makes no duplicate.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ExistingRecord_IsNotDuplicated()
    {
        var plugin = $"stub-{Guid.NewGuid():N}";
        using var host = HostWith(plugin);
        var invoiceId = await SeedUnpaidAsync(host, digital: true);
        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.FulfilmentRecords.Add(FulfilmentRecord.Create(invoiceId, plugin, FulfilmentKind.Paid, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(CancellationToken.None);
        }

        await MutateAsync(host, invoiceId, i => i.Settle("manual", Reference, DateTimeOffset.UtcNow, TransactionSource.Manual));

        (await RecordsAsync(host, invoiceId)).Should().ContainSingle();
    }

    /// <summary>With no fulfilment plugin enabled a payment writes no record.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task NoEnabledPlugin_WritesNoRecord()
    {
        using var host = HostWith();
        var invoiceId = await SeedUnpaidAsync(host, digital: true);

        await MutateAsync(host, invoiceId, i => i.Settle("manual", Reference, DateTimeOffset.UtcNow, TransactionSource.Manual));

        (await RecordsAsync(host, invoiceId)).Should().BeEmpty();
    }

    /// <summary>An invoice with no digital line owes the plugins nothing.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task InvoiceWithoutDigitalLine_WritesNoRecord()
    {
        using var host = HostWith($"stub-{Guid.NewGuid():N}");
        var invoiceId = await SeedUnpaidAsync(host, digital: false);

        await MutateAsync(host, invoiceId, i => i.Settle("manual", Reference, DateTimeOffset.UtcNow, TransactionSource.Manual));

        (await RecordsAsync(host, invoiceId)).Should().BeEmpty();
    }

    /// <summary>A save in a scope that discards domain events writes no record.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task SuppressedDomainEvents_WriteNoRecord()
    {
        using var host = HostWith($"stub-{Guid.NewGuid():N}");
        var invoiceId = await SeedUnpaidAsync(host, digital: true);

        using (var scope = host.Services.CreateScope())
        {
            using var suppression = scope.ServiceProvider.GetRequiredService<IDomainEventGate>().Suppress();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invoice = await db.Invoices.Include(i => i.Items).Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId);
            invoice.Settle("manual", Reference, DateTimeOffset.UtcNow, TransactionSource.Manual);
            await db.SaveChangesAsync(CancellationToken.None);
        }

        (await RecordsAsync(host, invoiceId)).Should().BeEmpty();
    }

    /// <summary>Builds a host whose enabled fulfilment plugins are exactly the given ids.</summary>
    /// <param name="pluginIds">The plugin ids to enable; none leaves the plugins directory empty.</param>
    /// <returns>The host; the caller disposes it.</returns>
    private WebApplicationFactory<Program> HostWith(params string[] pluginIds)
    {
        var resolver = new FakeFulfilmentPluginResolver();
        foreach (var id in pluginIds)
        {
            resolver.Register(id, new StubFulfilmentPlugin());
        }

        return factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IFulfilmentPluginResolver>();
            services.AddSingleton<IFulfilmentPluginResolver>(resolver);
        }));
    }

    /// <summary>Saves a client and an unpaid invoice with one line.</summary>
    /// <param name="host">The host to save through.</param>
    /// <param name="digital">Whether the line charges for a digital product.</param>
    /// <returns>The invoice id.</returns>
    private static async Task<int> SeedUnpaidAsync(WebApplicationFactory<Program> host, bool digital)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = Client.Create(
            $"subject-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        db.Clients.Add(client);
        await db.SaveChangesAsync(CancellationToken.None);

        var now = DateTimeOffset.UtcNow;
        var invoice = Invoice.Create(client.Id, now.AddDays(1), "EUR", now);
        invoice.AddItem("Licence", 10m, 2, null, new ProductSnapshot(42, BillingCycle.Annual, digital, "licence", null));
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }

    /// <summary>Loads an invoice in a scope of its own, applies a change and saves it.</summary>
    /// <param name="host">The host to save through.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="change">The domain change to apply.</param>
    /// <returns>A task that completes when the change is saved.</returns>
    private static async Task MutateAsync(WebApplicationFactory<Program> host, int invoiceId, Action<Invoice> change)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invoice = await db.Invoices.Include(i => i.Items).Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId);
        change(invoice);
        await db.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Reads the records of an invoice as the database holds them.</summary>
    /// <param name="host">The host to read through.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The records, untracked.</returns>
    private static async Task<List<FulfilmentRecord>> RecordsAsync(WebApplicationFactory<Program> host, int invoiceId)
    {
        using var scope = host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().FulfilmentRecords
            .AsNoTracking()
            .Where(r => r.InvoiceId == invoiceId)
            .ToListAsync(CancellationToken.None);
    }
}
