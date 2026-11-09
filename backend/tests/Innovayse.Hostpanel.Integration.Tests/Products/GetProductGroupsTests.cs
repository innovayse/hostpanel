using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>GET /api/products/groups</c> against Postgres: the staff-only gate, and the query's own
/// default for <c>activeOnly</c>.
/// </summary>
/// <remarks>
/// Pins the query's default: <c>GetProductGroupsQuery</c> itself defaults <c>activeOnly</c> to <c>true</c>.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetProductGroupsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>GET lists groups the operator switched off, so an anonymous caller gets 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ReturnsUnauthorized_WithoutAuth()
    {
        var response = await factory.CreateClient().GetAsync("/api/products/groups?activeOnly=false");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>An admin reads the staff list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ReturnsOk_ForAdmin()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/products/groups");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Omitting <c>activeOnly</c> from the query string still hides a group made inactive: the
    /// query's own default has to be <c>true</c> for a bound record, since a genuinely absent
    /// value binds to the type's default.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_OmittedActiveOnly_HidesAnInactiveGroup()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var created = await client.PostAsJsonAsync(
            "/api/products/groups", new { name = $"inactive-{Guid.NewGuid():N}" });
        var id = await created.Content.ReadFromJsonAsync<int>();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var group = await db.Set<ProductGroup>().FirstAsync(g => g.Id == id);
            group.Deactivate();
            await db.SaveChangesAsync();
        }

        var defaultList = await client.GetFromJsonAsync<JsonElement[]>("/api/products/groups");
        defaultList!.Should().NotContain(g => g.GetProperty("id").GetInt32() == id);

        var fullList = await client.GetFromJsonAsync<JsonElement[]>("/api/products/groups?activeOnly=false");
        fullList!.Should().Contain(g => g.GetProperty("id").GetInt32() == id);
    }
}
