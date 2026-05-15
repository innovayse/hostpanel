namespace Innovayse.Integration.Tests.Products;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Innovayse.Domain.Products;

/// <summary>Integration tests for /api/products and /api/products/groups endpoints.</summary>
public sealed class ProductsEndpointTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>
{
    /// <summary>GET /api/products is publicly accessible without authentication.</summary>
    [Fact]
    public async Task GetProductsAsync_ReturnsOk_WithoutAuthAsync()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Admin can create a product group and receives 201 Created with the new ID.</summary>
    [Fact]
    public async Task CreateProductGroupAsync_ReturnsCreated_AsAdminAsync()
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/products/groups",
            new { name = "Shared Hosting", description = "Affordable hosting plans" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<int>();
        id.Should().BeGreaterThan(0);
    }

    /// <summary>POST /api/products/groups without auth returns 401 Unauthorized.</summary>
    [Fact]
    public async Task CreateProductGroupAsync_Returns401_WithoutAuthAsync()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/products/groups",
            new { name = "Unauthorized Group" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Admin can create a product inside an existing group and receives 201 Created.</summary>
    [Fact]
    public async Task CreateProductAsync_ReturnsCreated_AsAdminAsync()
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Create parent group first
        var groupResponse = await client.PostAsJsonAsync("/api/products/groups",
            new { name = "VPS" });
        groupResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var groupId = await groupResponse.Content.ReadFromJsonAsync<int>();

        // Create product in that group
        var productResponse = await client.PostAsJsonAsync("/api/products",
            new { groupId, name = "VPS-1", description = "Entry VPS", type = ProductType.Vps, monthlyPrice = 9.99m, annualPrice = 99.99m });

        productResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var productId = await productResponse.Content.ReadFromJsonAsync<int>();
        productId.Should().BeGreaterThan(0);
    }

    /// <summary>POST /api/products without auth returns 401 Unauthorized.</summary>
    [Fact]
    public async Task CreateProductAsync_Returns401_WithoutAuthAsync()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/products",
            new { groupId = 1, name = "No-Auth Product", type = ProductType.SharedHosting, monthlyPrice = 5m, annualPrice = 50m });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
