using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/product-suspensions</c> against Postgres: one row per suspended service.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetProductSuspensionsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A suspended service appears in the Product Suspensions report.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ShowsASuspendedService()
    {
        var marker = $"Suspend{Guid.NewGuid():N}";
        var clientId = await ReportsTestSeeds.CreateClientAsync(factory, marker);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var group = ProductGroup.Create($"SuspGroup-{marker}", null);
            db.ProductGroups.Add(group);
            await db.SaveChangesAsync();
            var product = Product.Create(
                group.Id, $"SuspProduct-{marker}", null, null, null, null, ProductType.SharedHosting, 10m, 100m, DateTimeOffset.UtcNow);
            db.Products.Add(product);
            await db.SaveChangesAsync();

            var service = ClientService.Create(clientId, product.Id, BillingCycle.Monthly, DateTimeOffset.UtcNow);
            await ServicesTestSeeds.AddActivatedAsync(db, service, "provisioning-ref", DateTimeOffset.UtcNow);
            service.Suspend(DateTimeOffset.UtcNow, "non-payment");
            await db.SaveChangesAsync();
        }

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/product-suspensions");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        rows!.Should().Contain(
            r => r.GetProperty("clientName").GetString()!.Contains(marker)
                && r.GetProperty("productName").GetString()!.Contains(marker));
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/product-suspensions");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "product-suspensions");

        var response = await client.GetAsync("/api/reports/product-suspensions");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
