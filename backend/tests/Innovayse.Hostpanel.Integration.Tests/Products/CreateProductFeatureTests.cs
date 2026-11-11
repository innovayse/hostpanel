using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>POST /api/product-features</c> against Postgres: the missing-product and duplicate-label
/// refusals, and the authorization boundary.
/// </summary>
/// <remarks>
/// Pins the <c>PRODUCT_NOT_FOUND</c> refusal (404) and the duplicate-label rule under its own code,
/// <c>PRODUCT_FEATURE_LABEL_DUPLICATE</c>.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateProductFeatureTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>POST without auth answers 401 Unauthorized.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/product-features", new { productId = 1, label = "Disk", value = "10 GB", sortOrder = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Adding a line to an unknown product answers 404 with <c>PRODUCT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownProduct_RefusesNotFound()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/product-features", new { productId = 999_999, label = "Disk", value = "10 GB", sortOrder = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_NOT_FOUND);
    }

    /// <summary>A second line under the same label on the same product answers 400 with the duplicate code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DuplicateLabel_RefusesWithDuplicateCode()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);

        var first = await client.PostAsJsonAsync(
            "/api/product-features", new { productId, label = "Disk", value = "10 GB", sortOrder = 0 });
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync(
            "/api/product-features", new { productId, label = "Disk", value = "20 GB", sortOrder = 1 });

        second.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ProductsTestSeeds.AssertCodeAsync(second, HostpanelErrorCodes.PRODUCT_FEATURE_LABEL_DUPLICATE);
    }

    /// <summary>A line added to an existing product is created and reported with 201 Created.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreatesALine()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/product-features", new { productId, label = "Bandwidth", value = "1 TB", sortOrder = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<int>();
        id.Should().BeGreaterThan(0);
    }
}
