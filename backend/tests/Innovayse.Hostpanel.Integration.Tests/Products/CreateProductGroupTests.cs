using System.Net;
using System.Net.Http.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>POST /api/products/groups</c> against Postgres: the happy path and the authorization
/// boundary.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateProductGroupTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Admin can create a product group and receives 201 Created with the new id.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateProductGroupAsync_ReturnsCreated_AsAdminAsync()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/products/groups",
            new { name = "Shared Hosting", description = "Affordable hosting plans" });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<int>();
        id.Should().BeGreaterThan(0);
    }

    /// <summary>POST /api/products/groups without auth returns 401 Unauthorized.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateProductGroupAsync_Returns401_WithoutAuthAsync()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/products/groups",
            new { name = "Unauthorized Group" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
