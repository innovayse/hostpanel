using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>PUT /api/product-features/{id}</c> against Postgres: the missing-line and duplicate-label
/// refusals, and the authorization boundary.
/// </summary>
/// <remarks>
/// Pins the
/// <c>PRODUCT_FEATURE_NOT_FOUND</c> refusal (404)
/// and the rename-onto-another-line's-label rule, excluding the line under edit from its own
/// check.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateProductFeatureTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>PUT without auth answers 401 Unauthorized.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync(
            "/api/product-features/1", new { label = "Disk", value = "10 GB", sortOrder = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Updating an unknown line id answers 404 with <c>PRODUCT_FEATURE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownId_RefusesNotFound()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            "/api/product-features/999999", new { label = "Disk", value = "10 GB", sortOrder = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_FEATURE_NOT_FOUND);
    }

    /// <summary>Renaming a line onto a label another line of the same product already uses is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenameOntoAnotherLinesLabel_RefusesWithDuplicateCode()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);

        var disk = await client.PostAsJsonAsync(
            "/api/product-features", new { productId, label = "Disk", value = "10 GB", sortOrder = 0 });
        var diskId = await disk.Content.ReadFromJsonAsync<int>();
        var bandwidth = await client.PostAsJsonAsync(
            "/api/product-features", new { productId, label = "Bandwidth", value = "1 TB", sortOrder = 1 });
        var bandwidthId = await bandwidth.Content.ReadFromJsonAsync<int>();

        var response = await client.PutAsJsonAsync(
            $"/api/product-features/{bandwidthId}", new { label = "Disk", value = "1 TB", sortOrder = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_FEATURE_LABEL_DUPLICATE);
        diskId.Should().NotBe(bandwidthId);
    }

    /// <summary>Saving a line without renaming it succeeds: the line under edit excludes itself from the check.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WithoutRenaming_Succeeds()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var productId = await ProductsTestSeeds.CreateProductAsync(client);
        var created = await client.PostAsJsonAsync(
            "/api/product-features", new { productId, label = "Disk", value = "10 GB", sortOrder = 0 });
        var id = await created.Content.ReadFromJsonAsync<int>();

        var response = await client.PutAsJsonAsync(
            $"/api/product-features/{id}", new { label = "Disk", value = "20 GB", sortOrder = 0 });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
