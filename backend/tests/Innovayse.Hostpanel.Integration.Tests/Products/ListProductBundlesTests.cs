using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>GET /api/products/bundles</c> against Postgres: the staff list, which carries the bundles the
/// operator switched off.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListProductBundlesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The list holds active and inactive bundles, each with its component names.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ReturnsActiveAndInactiveBundlesWithComponentNames()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var product = await ProductsTestSeeds.CreateProductAsync(client);
        var activeId = await ProductsTestSeeds.CreateBundleAsync(client, ProductsTestSeeds.BundleBody(product));
        var inactiveId = await ProductsTestSeeds.CreateBundleAsync(client, ProductsTestSeeds.BundleBody(product));
        var off = ProductsTestSeeds.BundleBody(product);
        off["isActive"] = false;
        (await client.PutAsJsonAsync($"/api/products/bundles/{inactiveId}", off)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await client.GetFromJsonAsync<JsonElement[]>("/api/products/bundles");

        list!.Single(b => b.GetProperty("id").GetInt32() == activeId).GetProperty("isActive").GetBoolean().Should().BeTrue();
        var inactive = list!.Single(b => b.GetProperty("id").GetInt32() == inactiveId);
        inactive.GetProperty("isActive").GetBoolean().Should().BeFalse();
        inactive.GetProperty("components")[0].GetProperty("name").GetString().Should().Be("Featured");
    }

    /// <summary>The list is ordered by sort position first.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_OrdersBySortOrder()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var product = await ProductsTestSeeds.CreateProductAsync(client);
        var late = ProductsTestSeeds.BundleBody(product);
        late["sortOrder"] = 900;
        var early = ProductsTestSeeds.BundleBody(product);
        early["sortOrder"] = 800;
        var lateId = await ProductsTestSeeds.CreateBundleAsync(client, late);
        var earlyId = await ProductsTestSeeds.CreateBundleAsync(client, early);

        var list = await client.GetFromJsonAsync<JsonElement[]>("/api/products/bundles");

        var ids = list!.Select(b => b.GetProperty("id").GetInt32()).ToList();
        ids.IndexOf(earlyId).Should().BeLessThan(ids.IndexOf(lateId));
    }

    /// <summary>GET /api/products/bundles without auth returns 401 Unauthorized.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_Returns401_WithoutAuth()
    {
        var response = await factory.CreateClient().GetAsync("/api/products/bundles");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
