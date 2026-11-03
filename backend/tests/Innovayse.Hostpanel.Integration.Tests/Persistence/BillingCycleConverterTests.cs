using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Persistence;

/// <summary>
/// <see cref="BillingCycleConverter"/> reads every spelling older rows hold and writes one canonical
/// text, both on its own and through the <c>ClientServices</c> and <c>OrderItems</c> columns.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class BillingCycleConverterTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Every spelling the platform has written reads back as its cycle, whatever the casing.</summary>
    /// <param name="stored">The column value.</param>
    /// <param name="expected">The cycle it names.</param>
    [Theory]
    [InlineData("monthly", BillingCycle.Monthly)]
    [InlineData("Monthly", BillingCycle.Monthly)]
    [InlineData("annual", BillingCycle.Annual)]
    [InlineData("Annual", BillingCycle.Annual)]
    [InlineData("annually", BillingCycle.Annual)]
    [InlineData("Annually", BillingCycle.Annual)]
    [InlineData("Quarterly", BillingCycle.Quarterly)]
    [InlineData("quarterly", BillingCycle.Quarterly)]
    [InlineData("SemiAnnual", BillingCycle.SemiAnnual)]
    [InlineData("semi-annual", BillingCycle.SemiAnnual)]
    [InlineData("Biennial", BillingCycle.Biennial)]
    public void FromStored_EveryLegacySpelling_ReadsTheCycle(string stored, BillingCycle expected)
    {
        BillingCycleConverter.FromStored(stored).Should().Be(expected);
    }

    /// <summary>A text that names no cycle fails the read instead of guessing one.</summary>
    /// <param name="stored">The column value.</param>
    [Theory]
    [InlineData("free")]
    [InlineData("weekly")]
    [InlineData("")]
    public void FromStored_UnknownText_Throws(string stored)
    {
        var act = () => BillingCycleConverter.FromStored(stored);

        act.Should().Throw<InvalidOperationException>();
    }

    /// <summary>Each cycle is written as its member name.</summary>
    /// <param name="cycle">The cycle.</param>
    /// <param name="expected">The canonical column value.</param>
    [Theory]
    [InlineData(BillingCycle.Monthly, "Monthly")]
    [InlineData(BillingCycle.Annual, "Annual")]
    [InlineData(BillingCycle.Quarterly, "Quarterly")]
    [InlineData(BillingCycle.SemiAnnual, "SemiAnnual")]
    [InlineData(BillingCycle.Biennial, "Biennial")]
    public void ToStored_WritesTheCanonicalText(BillingCycle cycle, string expected)
    {
        BillingCycleConverter.ToStored(cycle).Should().Be(expected);
    }

    /// <summary>A service row holding a legacy spelling loads as its cycle.</summary>
    /// <param name="legacy">The legacy column value written straight into the row.</param>
    /// <param name="expected">The cycle the row must load as.</param>
    [Theory]
    [InlineData("monthly", BillingCycle.Monthly)]
    [InlineData("annual", BillingCycle.Annual)]
    [InlineData("annually", BillingCycle.Annual)]
    [InlineData("Quarterly", BillingCycle.Quarterly)]
    [InlineData("SemiAnnual", BillingCycle.SemiAnnual)]
    [InlineData("Biennial", BillingCycle.Biennial)]
    public async Task ClientServices_LegacyRow_LoadsAsItsCycleAsync(string legacy, BillingCycle expected)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var productId = await DomainsTestSeeds.SeedHostingProductAsync(context);
        var service = ClientService.Create(clientId, productId, BillingCycle.Monthly, DateTimeOffset.UtcNow);
        context.Set<ClientService>().Add(service);
        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlRawAsync("UPDATE \"ClientServices\" SET \"BillingCycle\" = {0} WHERE \"Id\" = {1}", legacy, service.Id);

        using var readScope = factory.Services.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var loaded = await readContext.Set<ClientService>().SingleAsync(s => s.Id == service.Id);
        loaded.BillingCycle.Should().Be(expected);
    }

    /// <summary>An order line is stored with the canonical cycle text and the domain action's exact text.</summary>
    [Fact]
    public async Task OrderItems_WriteTheCanonicalCycleAndTheExactActionTextAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var productId = await DomainsTestSeeds.SeedHostingProductAsync(context);
        var order = Order.Create($"ORD-{Guid.NewGuid():N}"[..12], clientId, "bank_transfer", null, "USD", DateTimeOffset.UtcNow);
        order.AddItem(productId, "Domain", BillingCycle.Annual, 1m, 1m, "example.test", null, DomainAction.Renew, null, 1);
        context.Set<Order>().Add(order);
        await context.SaveChangesAsync();
        var itemId = order.Items[0].Id;

        (await RawAsync(context, "OrderItems", itemId)).Should().Be("Annual");
        var action = await context.Database
            .SqlQueryRaw<string>("SELECT \"DomainAction\" AS \"Value\" FROM \"OrderItems\" WHERE \"Id\" = {0}", itemId)
            .SingleAsync();
        action.Should().Be("renew");
    }

    /// <summary>Reads the raw <c>BillingCycle</c> column of one row.</summary>
    /// <param name="context">The context to query through.</param>
    /// <param name="table">The table name.</param>
    /// <param name="id">The row id.</param>
    /// <returns>The stored text.</returns>
    private static async Task<string> RawAsync(AppDbContext context, string table, int id)
    {
        // A table name cannot be a parameter; it comes from this test's own literals, the id stays a parameter.
        var sql = $"SELECT \"BillingCycle\" AS \"Value\" FROM \"{table}\" WHERE \"Id\" = {{0}}";
        return await context.Database.SqlQueryRaw<string>(sql, id).SingleAsync();
    }
}
