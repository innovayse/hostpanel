using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>GET /api/products/bundles/{id}</c> against Postgres: one bundle for the edit screen.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetProductBundleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The bundle comes back with its staff fields and its components.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_ReturnsTheBundleWithItsComponents()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var product = await ProductsTestSeeds.CreateProductAsync(client);
        var id = await ProductsTestSeeds.CreateBundleAsync(client, ProductsTestSeeds.BundleBody(product));

        var bundle = await client.GetFromJsonAsync<JsonElement>($"/api/products/bundles/{id}");

        bundle.GetProperty("id").GetInt32().Should().Be(id);
        bundle.GetProperty("isActive").GetBoolean().Should().BeTrue();
        bundle.GetProperty("sortOrder").GetInt32().Should().Be(0);
        bundle.GetProperty("components").GetArrayLength().Should().Be(1);
        bundle.GetProperty("components")[0].GetProperty("productId").GetInt32().Should().Be(product);
    }

    /// <summary>An unknown id answers 404 with <c>PRODUCT_BUNDLE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_UnknownId_RefusesNotFound()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/products/bundles/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_BUNDLE_NOT_FOUND);
    }

    /// <summary>GET /api/products/bundles/{id} without auth returns 401 Unauthorized.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Get_Returns401_WithoutAuth()
    {
        var response = await factory.CreateClient().GetAsync("/api/products/bundles/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
