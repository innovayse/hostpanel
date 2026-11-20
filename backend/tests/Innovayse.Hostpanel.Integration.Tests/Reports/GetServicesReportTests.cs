using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/services</c> against Postgres: the paginated services report.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetServicesReportTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The ISO 4217 code of the non-base currency one seeded client is billed in.</summary>
    private const string ForeignCurrency = "XTS";

    /// <summary>The ISO 4217 numeric code of <see cref="ForeignCurrency"/>.</summary>
    private const string ForeignNumeric = "963";

    /// <summary>What one unit of <see cref="ForeignCurrency"/> is worth in the base currency.</summary>
    private const decimal ForeignRate = 0.5m;

    /// <summary>A freshly created service appears in the paginated services report with its client and product names and the currency of its amounts.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ShowsAService()
    {
        var marker = $"Svc{Guid.NewGuid():N}";
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, marker);
        string baseCode;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            baseCode = (await db.Currencies.SingleAsync(c => c.IsBase)).Code;
            var group = ProductGroup.Create($"SvcGroup-{marker}", null);
            db.ProductGroups.Add(group);
            await db.SaveChangesAsync();
            var product = Product.Create(
                group.Id, $"SvcProduct-{marker}", null, null, null, null, ProductType.SharedHosting, 10m, 100m, TestMoments.Seeded);
            db.Products.Add(product);
            await db.SaveChangesAsync();

            var service = ClientService.Create(clientId, product.Id, BillingCycle.Monthly, TestMoments.Seeded);
            db.ClientServices.Add(service);
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/services?page=1&pageSize=200");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").EnumerateArray().Should().Contain(
            i => i.GetProperty("clientName").GetString()!.Contains(marker)
                && i.GetProperty("productName").GetString()!.Contains(marker)
                && i.GetProperty("currencyCode").GetString() == baseCode);
    }

    /// <summary>A service owned by a client billed in a non-base currency shows that client's currency code, not the base.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ShowsTheOwnersBillingCurrency()
    {
        var marker = $"SvcForeign{Guid.NewGuid():N}";
        await BillingTestSeeds.EnsureCurrencyAsync(factory, ForeignCurrency, ForeignNumeric, ForeignRate);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory, ForeignCurrency, marker);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var group = ProductGroup.Create($"SvcGroup-{marker}", null);
            db.ProductGroups.Add(group);
            await db.SaveChangesAsync();
            var product = Product.Create(
                group.Id, $"SvcProduct-{marker}", null, null, null, null, ProductType.SharedHosting, 10m, 100m, TestMoments.Seeded);
            db.Products.Add(product);
            await db.SaveChangesAsync();

            db.ClientServices.Add(ClientService.Create(clientId, product.Id, BillingCycle.Monthly, TestMoments.Seeded));
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/services?page=1&pageSize=200");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").EnumerateArray().Should().Contain(
            i => i.GetProperty("clientName").GetString()!.Contains(marker)
                && i.GetProperty("currencyCode").GetString() == ForeignCurrency);
    }

    /// <summary>A page size outside the allowed range is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnOversizedPage()
    {
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/reports/services?page=1&pageSize=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/services?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "services-report");

        var response = await client.GetAsync("/api/reports/services?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
