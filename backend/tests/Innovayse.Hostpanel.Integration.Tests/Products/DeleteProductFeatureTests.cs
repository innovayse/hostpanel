using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>DELETE /api/product-features/{id}</c> against Postgres: the happy path, the missing-line
/// refusal, and the authorization boundary.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteProductFeatureTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>DELETE without auth answers 401 Unauthorized.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().DeleteAsync("/api/product-features/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Deleting an unknown line id answers 404 with <c>PRODUCT_FEATURE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownId_RefusesNotFound()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync("/api/product-features/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_FEATURE_NOT_FOUND);
    }

    /// <summary>A deleted line no longer appears in the product's list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemovesTheLineFromTheList()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);
        var created = await client.PostAsJsonAsync(
            "/api/product-features", new { productId, label = "Disk", value = "10 GB", sortOrder = 0 });
        var id = await created.Content.ReadFromJsonAsync<int>();

        var deleted = await client.DeleteAsync($"/api/product-features/{id}");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await client.GetFromJsonAsync<JsonElement[]>($"/api/product-features?productId={productId}");
        list!.Should().BeEmpty();
    }
}
