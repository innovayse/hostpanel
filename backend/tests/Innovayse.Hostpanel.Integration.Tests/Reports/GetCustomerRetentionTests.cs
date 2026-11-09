using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/customer-retention</c> against Postgres: average retention per product,
/// grouped by product group.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetCustomerRetentionTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Retention answers with a shape even when nothing has ever churned.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnswersWithoutRefusing()
    {
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/reports/customer-retention");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("groups", out _).Should().BeTrue();
    }

    /// <summary>
    /// Ended domains of one TLD are counted and their lifetimes averaged in SQL: lifetimes of 100
    /// and 200 days average to 150.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AveragesTheLifetimesOfOneTld()
    {
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, $"Retention{Guid.NewGuid():N}");
        var registered = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Domains.Add(RegisteredDomain.Import(
                clientId, $"a{Guid.NewGuid():N}.rtest", DomainStatus.Expired, registered, registered.AddDays(100), false, "USD", null));
            db.Domains.Add(RegisteredDomain.Import(
                clientId, $"b{Guid.NewGuid():N}.rtest", DomainStatus.Cancelled, registered, registered.AddDays(200), false, "USD", null));
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/customer-retention?includeActive=false");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var group = body.GetProperty("groups").EnumerateArray()
            .Single(g => g.GetProperty("kind").GetString() == "Domains");
        group.GetProperty("groupName").ValueKind.Should().Be(JsonValueKind.Null);
        var row = group.GetProperty("rows").EnumerateArray()
            .Single(r => r.GetProperty("productName").GetString()!.Contains("rtest", StringComparison.Ordinal));
        row.GetProperty("productCount").GetInt32().Should().Be(2);
        row.GetProperty("avgDaysActive").GetInt32().Should().Be(150);
        row.GetProperty("avgYears").GetInt32().Should().Be(0);
        row.GetProperty("avgMonths").GetInt32().Should().Be(5);
        row.GetProperty("termYears").GetInt32().Should().Be(1);
        row.GetProperty("billingCycle").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/customer-retention");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "customer-retention");

        var response = await client.GetAsync("/api/reports/customer-retention");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
