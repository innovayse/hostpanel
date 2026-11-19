using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/new-customers</c> against Postgres: twelve months of signups, orders and
/// conversion rate.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class NewCustomersTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Twelve months come back regardless of whether anything happened.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReturnsTwelveMonths()
    {
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/reports/new-customers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        rows.Should().HaveCount(12);
    }

    /// <summary>Orders placed and orders completed differ: only an accepted (active) order counts as completed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CountsOnlyActiveOrdersAsCompleted()
    {
        const int year = 2043;
        var placedAt = new DateTimeOffset(year, 6, 10, 12, 0, 0, TimeSpan.Zero);
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"Completed{Guid.NewGuid():N}");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pending = Order.Create($"ORD{Guid.NewGuid():N}"[..20], clientId, "manual", null, "USD", placedAt);
            pending.Backdate(placedAt);
            var active = Order.Create($"ORD{Guid.NewGuid():N}"[..20], clientId, "manual", null, "USD", placedAt);
            active.Accept(placedAt);
            active.Backdate(placedAt);
            db.Orders.AddRange(pending, active);
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var rows = await client.GetFromJsonAsync<List<JsonElement>>($"/api/reports/new-customers?year={year}");

        var june = rows!.Single(r => r.GetProperty("month").GetString() == "Jun");
        june.GetProperty("ordersPlaced").GetInt32().Should().Be(2);
        june.GetProperty("ordersCompleted").GetInt32().Should().Be(1);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/new-customers");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "new-customers");

        var response = await client.GetAsync("/api/reports/new-customers");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
