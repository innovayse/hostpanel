using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>PUT /api/products/groups/{id}</c> against Postgres: the happy path and the missing-group
/// refusal.
/// </summary>
/// <remarks>
/// Pins the <c>PRODUCT_GROUP_NOT_FOUND</c>
/// refusal (404).
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateProductGroupTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Updating an unknown group id answers 404 with <c>PRODUCT_GROUP_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_UnknownId_RefusesNotFound()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync(
            "/api/products/groups/999999", new { name = "Ghost", description = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_GROUP_NOT_FOUND);
    }

    /// <summary>Creating then updating a group round-trips its new name and description.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateThenUpdate_RoundTrips()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var created = await client.PostAsJsonAsync(
            "/api/products/groups", new { name = $"group-{Guid.NewGuid():N}", description = "Before" });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = await created.Content.ReadFromJsonAsync<int>();

        var updated = await client.PutAsJsonAsync(
            $"/api/products/groups/{id}", new { name = "Renamed", description = "After" });
        updated.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await client.GetFromJsonAsync<JsonElement[]>("/api/products/groups?activeOnly=false");
        var group = list!.Single(g => g.GetProperty("id").GetInt32() == id);
        group.GetProperty("name").GetString().Should().Be("Renamed");
        group.GetProperty("description").GetString().Should().Be("After");
    }

    /// <summary>PUT /api/products/groups/{id} without auth returns 401 Unauthorized.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_Returns401_WithoutAuth()
    {
        var response = await factory.CreateClient().PutAsJsonAsync(
            "/api/products/groups/1", new { name = "No-Auth" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
