using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin;

/// <summary>
/// <c>GET /api/admin/reports/service-usage</c>: an admin reads the usage items grouped
/// by product, a caller with only the Client role is refused, and the route is closed to a caller
/// with no credential. This use case refuses nothing once authorized.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetServiceUsageReportTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin reads the service-usage report.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanReadTheReportAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/reports/service-usage");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>The report counts the services of each product and names the product.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CountsServicesPerProductAsync()
    {
        var name = $"Usage-{Guid.NewGuid():N}";
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var group = ProductGroup.Create($"Group-{Guid.NewGuid():N}", null);
            context.ProductGroups.Add(group);
            await context.SaveChangesAsync(CancellationToken.None);
            var product = Product.Create(group.Id, name, null, null, null, null, ProductType.SharedHosting, 5m, 50m, TestMoments.Seeded);
            var client = Client.Create($"user-{Guid.NewGuid():N}", "Ada", "Lovelace", $"ada-{Guid.NewGuid():N}@example.test", TestMoments.Seeded);
            context.Products.Add(product);
            context.Clients.Add(client);
            await context.SaveChangesAsync(CancellationToken.None);
            context.ClientServices.Add(ClientService.Create(client.Id, product.Id, BillingCycle.Monthly, TestMoments.Seeded));
            context.ClientServices.Add(ClientService.Create(client.Id, product.Id, BillingCycle.Monthly, TestMoments.Seeded));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var admin = await AdminTestSeeds.AdminClientAsync(factory);
        var response = await admin.GetAsync("/api/admin/reports/service-usage");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.EnumerateArray()
            .Single(item => item.GetProperty("productName").GetString() == name)
            .GetProperty("count").GetInt32().Should().Be(2);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await AdminTestSeeds.ClientRoleClientAsync(factory, "service-usage-forbidden");

        var response = await client.GetAsync("/api/admin/reports/service-usage");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The service-usage report is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/reports/service-usage");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
