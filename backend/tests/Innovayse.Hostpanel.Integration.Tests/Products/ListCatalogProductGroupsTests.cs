using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>GET /api/public/product-groups</c> against Postgres: the storefront's group list anyone may
/// read, which lists only active groups and carries no active flag.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListCatalogProductGroupsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An anonymous caller reads the list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListCatalog_ReturnsOk_WithoutAuthAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/public/product-groups");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// An inactive group is never listed, an active one is, and no row carries the active flag.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListCatalog_ReturnsOnlyActiveGroupsWithoutActiveFlagAsync()
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        var activeId = await ProductsTestSeeds.CreateGroupAsync(admin);
        var inactiveId = await ProductsTestSeeds.CreateGroupAsync(admin);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var group = await db.Set<ProductGroup>().FirstAsync(g => g.Id == inactiveId);
            group.Deactivate();
            await db.SaveChangesAsync();
        }

        var list = await factory.CreateClient().GetFromJsonAsync<JsonElement[]>("/api/public/product-groups");

        list!.Should().Contain(g => g.GetProperty("id").GetInt32() == activeId);
        list.Should().NotContain(g => g.GetProperty("id").GetInt32() == inactiveId);
        list.Should().AllSatisfy(g => g.TryGetProperty("isActive", out _).Should().BeFalse());
    }
}
