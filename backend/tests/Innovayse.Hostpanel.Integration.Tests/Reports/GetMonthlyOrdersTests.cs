using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/monthly-orders</c> against Postgres: orders grouped by product group,
/// with group and overall totals.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMonthlyOrdersTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An active order for a product in an active group is counted in the Monthly Orders report.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CountsAnActiveOrder()
    {
        var marker = $"Order{Guid.NewGuid():N}";
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, marker);
        int productId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var group = ProductGroup.Create($"Group-{marker}", null);
            db.ProductGroups.Add(group);
            await db.SaveChangesAsync();

            var product = Product.Create(
                group.Id, $"Product-{marker}", null, null, null, null, ProductType.SharedHosting, 10m, 100m, TestMoments.Seeded);
            db.Products.Add(product);
            await db.SaveChangesAsync();
            productId = product.Id;

            var order = Order.Create($"ORD{Guid.NewGuid():N}"[..20], clientId, "manual", null, "USD", TestMoments.Seeded);
            order.AddItem(productId, product.Name, BillingCycle.Monthly, 10m, 10m, null, null);
            order.Accept(TestMoments.Seeded);
            // The stored time of an order is set at save; put it in the requested month.
            order.Backdate(TestMoments.Seeded);
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/monthly-orders?year=2026&month=3");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("groups").EnumerateArray().Should().Contain(
            g => g.GetProperty("groupName").GetString() == $"Group-{marker}"
                && g.GetProperty("products").EnumerateArray().Any(
                    p => p.GetProperty("productId").GetInt32() == productId && p.GetProperty("unitsSold").GetInt32() == 1));
    }

    /// <summary>
    /// A unit is an order line on a pending or accepted order, never a cancelled one, and the
    /// foreign order's value is converted: 30 plus 8,000 (20 in the base) is 50.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CountsPendingAndAcceptedLinesInTheBaseCurrency()
    {
        var scenario = await IncomeTestSeeds.SeedAsync(factory, 2038);
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var body = await client.GetFromJsonAsync<JsonElement>("/api/reports/monthly-orders?year=2038&month=5");

        var row = body.GetProperty("groups").EnumerateArray()
            .SelectMany(g => g.GetProperty("products").EnumerateArray())
            .Single(p => p.GetProperty("productId").GetInt32() == scenario.ProductId);
        row.GetProperty("unitsSold").GetInt32().Should().Be(2);
        row.GetProperty("value").GetDecimal().Should().Be(50m);
        body.GetProperty("currency").GetString().Should().Be(scenario.BaseCurrency);
    }

    /// <summary>An out-of-range month is refused before the handler runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnInvalidMonth()
    {
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/reports/monthly-orders?year=2024&month=13");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/monthly-orders");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "monthly-orders");

        var response = await client.GetAsync("/api/reports/monthly-orders");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
