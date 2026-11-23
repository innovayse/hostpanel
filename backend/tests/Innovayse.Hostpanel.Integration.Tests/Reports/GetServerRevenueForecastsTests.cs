using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/server-revenue-forecasts</c> against Postgres: what the active services on each server renew for.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetServerRevenueForecastsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The ISO 4217 code of the non-base currency one seeded client is billed in.</summary>
    private const string ForeignCurrency = "XTS";

    /// <summary>The ISO 4217 numeric code of <see cref="ForeignCurrency"/>.</summary>
    private const string ForeignNumeric = "963";

    /// <summary>What one unit of <see cref="ForeignCurrency"/> is worth in the base currency.</summary>
    private const decimal ForeignRate = 0.5m;

    /// <summary>
    /// A monthly and an annual service on one server add up as monthly revenue, the annual one spread over twelve months,
    /// and the suspended one is left out; a server without services shows zero.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SpreadsEachActiveServiceOverItsCycle()
    {
        var marker = $"Forecast{Guid.NewGuid():N}";
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, marker);
        await BillingTestSeeds.EnsureCurrencyAsync(factory, ForeignCurrency, ForeignNumeric, ForeignRate);
        var foreignClientId = await BillingTestSeeds.SeedClientAsync(factory, ForeignCurrency);
        int busyServerId;
        int idleServerId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var group = ProductGroup.Create($"ForecastGroup-{marker}", null);
            db.ProductGroups.Add(group);
            await db.SaveChangesAsync();
            var product = Product.Create(
                group.Id, $"ForecastProduct-{marker}", null, null, null, null, ProductType.SharedHosting, 10m, 100m, TestMoments.Seeded);
            db.Products.Add(product);
            var busy = NewServer($"busy-{marker}");
            var idle = NewServer($"idle-{marker}");
            db.Servers.AddRange(busy, idle);
            await db.SaveChangesAsync();
            busyServerId = busy.Id;
            idleServerId = idle.Id;

            await ServicesTestSeeds.AddActiveServiceOnServerAsync(db, clientId, product.Id, busy.Id, BillingCycle.Monthly, 10m, suspend: false);
            await ServicesTestSeeds.AddActiveServiceOnServerAsync(db, clientId, product.Id, busy.Id, BillingCycle.Annual, 120m, suspend: false);
            await ServicesTestSeeds.AddActiveServiceOnServerAsync(db, foreignClientId, product.Id, busy.Id, BillingCycle.Monthly, 40m, suspend: false);
            await ServicesTestSeeds.AddActiveServiceOnServerAsync(db, clientId, product.Id, busy.Id, BillingCycle.Monthly, 500m, suspend: true);
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/server-revenue-forecasts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var rows = body.GetProperty("rows").EnumerateArray().ToList();
        body.GetProperty("currency").GetString().Should().HaveLength(3);
        var busyRow = rows.Single(r => r.GetProperty("serverId").GetInt32() == busyServerId);
        busyRow.GetProperty("activeServices").GetInt32().Should().Be(3);
        // 10 + 120 / 12 in the base currency, plus 40 of the foreign currency converted at its rate.
        var monthly = 20m + (40m * ForeignRate);
        busyRow.GetProperty("monthlyRevenue").GetDecimal().Should().Be(monthly);
        busyRow.GetProperty("annualRevenue").GetDecimal().Should().Be(monthly * 12m);
        var idleRow = rows.Single(r => r.GetProperty("serverId").GetInt32() == idleServerId);
        idleRow.GetProperty("activeServices").GetInt32().Should().Be(0);
        idleRow.GetProperty("monthlyRevenue").GetDecimal().Should().Be(0m);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/server-revenue-forecasts");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "server-revenue-forecasts");

        var response = await client.GetAsync("/api/reports/server-revenue-forecasts");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Builds a server with the given name and no other settings.</summary>
    /// <param name="name">The server's display name.</param>
    /// <returns>A new, unsaved server.</returns>
    private static Server NewServer(string name) =>
        Server.Create(
            new ServerDetails(
                Name: name,
                Hostname: $"{name}.example.test",
                IpAddress: null,
                AssignedIpAddresses: null,
                Module: ServerModules.CPanel,
                Username: "api",
                Credentials: new ServerCredentials("secret", null, null),
                UseSSL: true,
                MaxAccounts: null,
                IsDefault: false,
                IsDisabled: false,
                MonthlyCost: 0m,
                Datacenter: null,
                ServerStatusAddress: null,
                NameServers: ServerNameServers.None,
                AcceptsSelfSignedCertificate: false),
            TestMoments.Seeded);
}
