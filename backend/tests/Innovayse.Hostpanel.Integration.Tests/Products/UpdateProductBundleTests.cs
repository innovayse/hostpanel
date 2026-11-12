using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>PUT /api/products/bundles/{id}</c> against Postgres: the happy path, switching a bundle off, and the
/// refusals that must leave the stored bundle untouched.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateProductBundleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Updating replaces the details and the components, keeping the product that stays.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_ReplacesDetailsAndComponents()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var kept = await ProductsTestSeeds.CreateProductAsync(client);
        var dropped = await ProductsTestSeeds.CreateProductAsync(client);
        var added = await ProductsTestSeeds.CreateProductAsync(client);
        var id = await ProductsTestSeeds.CreateBundleAsync(client, ProductsTestSeeds.BundleBody(kept, dropped));

        var body = ProductsTestSeeds.BundleBody(added, kept);
        body["name"] = "Renamed";
        body["includesDomain"] = false;
        body["domainYears"] = null;
        body["discountType"] = "None";
        body["discountAmount"] = 0m;
        var response = await client.PutAsJsonAsync($"/api/products/bundles/{id}", body);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var bundle = await client.GetFromJsonAsync<JsonElement>($"/api/products/bundles/{id}");
        bundle.GetProperty("name").GetString().Should().Be("Renamed");
        bundle.GetProperty("includesDomain").GetBoolean().Should().BeFalse();
        bundle.GetProperty("isActive").GetBoolean().Should().BeTrue();
        var components = bundle.GetProperty("components").EnumerateArray().ToList();
        components.Select(c => c.GetProperty("productId").GetInt32()).Should().Equal(added, kept);
        components[0].GetProperty("isPrimary").GetBoolean().Should().BeTrue();
        components[1].GetProperty("isPrimary").GetBoolean().Should().BeFalse();
    }

    /// <summary>A body with <c>isActive: false</c> switches the bundle off.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_WithIsActiveFalse_DeactivatesTheBundle()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var product = await ProductsTestSeeds.CreateProductAsync(client);
        var id = await ProductsTestSeeds.CreateBundleAsync(client, ProductsTestSeeds.BundleBody(product));
        var body = ProductsTestSeeds.BundleBody(product);
        body["isActive"] = false;

        var response = await client.PutAsJsonAsync($"/api/products/bundles/{id}", body);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var bundle = await client.GetFromJsonAsync<JsonElement>($"/api/products/bundles/{id}");
        bundle.GetProperty("isActive").GetBoolean().Should().BeFalse();
    }

    /// <summary>A body that leaves <c>isActive</c> out keeps an inactive bundle off.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_WithoutIsActive_KeepsAnInactiveBundleOff()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var product = await ProductsTestSeeds.CreateProductAsync(client);
        var id = await ProductsTestSeeds.CreateBundleAsync(client, ProductsTestSeeds.BundleBody(product));
        var off = ProductsTestSeeds.BundleBody(product);
        off["isActive"] = false;
        (await client.PutAsJsonAsync($"/api/products/bundles/{id}", off)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await client.PutAsJsonAsync($"/api/products/bundles/{id}", ProductsTestSeeds.BundleBody(product));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var bundle = await client.GetFromJsonAsync<JsonElement>($"/api/products/bundles/{id}");
        bundle.GetProperty("isActive").GetBoolean().Should().BeFalse();
    }

    /// <summary>Updating an unknown bundle answers 404 with <c>PRODUCT_BUNDLE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_UnknownId_RefusesNotFound()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var product = await ProductsTestSeeds.CreateProductAsync(client);

        var response = await client.PutAsJsonAsync("/api/products/bundles/999999", ProductsTestSeeds.BundleBody(product));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_BUNDLE_NOT_FOUND);
    }

    /// <summary>A component naming no product answers 404 and the stored bundle keeps its components.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_UnknownProduct_RefusesAndKeepsTheBundle()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var product = await ProductsTestSeeds.CreateProductAsync(client);
        var id = await ProductsTestSeeds.CreateBundleAsync(client, ProductsTestSeeds.BundleBody(product));

        var response = await client.PutAsJsonAsync($"/api/products/bundles/{id}", ProductsTestSeeds.BundleBody(999999));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_BUNDLE_PRODUCT_NOT_FOUND);
        var bundle = await client.GetFromJsonAsync<JsonElement>($"/api/products/bundles/{id}");
        bundle.GetProperty("components").EnumerateArray().Select(c => c.GetProperty("productId").GetInt32())
            .Should().Equal(product);
    }

    /// <summary>An update with no primary component answers 400 and the stored bundle is unchanged.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_NoPrimary_RefusesAndKeepsTheBundle()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var product = await ProductsTestSeeds.CreateProductAsync(client);
        var createBody = ProductsTestSeeds.BundleBody(product);
        var id = await ProductsTestSeeds.CreateBundleAsync(client, createBody);
        var body = ProductsTestSeeds.BundleBody(product);
        body["name"] = "Never stored";
        body["items"] = new[] { new { productId = product, isPrimary = false } };

        var response = await client.PutAsJsonAsync($"/api/products/bundles/{id}", body);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_BUNDLE_PRIMARY_INVALID);
        var bundle = await client.GetFromJsonAsync<JsonElement>($"/api/products/bundles/{id}");
        bundle.GetProperty("name").GetString().Should().Be((string)createBody["name"]!);
    }

    /// <summary>PUT /api/products/bundles/{id} without auth returns 401 Unauthorized.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_Returns401_WithoutAuth()
    {
        var response = await factory.CreateClient().PutAsJsonAsync("/api/products/bundles/1", ProductsTestSeeds.BundleBody(1));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
