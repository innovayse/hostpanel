using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>GET /api/public/products</c> against Postgres: the storefront catalogue anyone may read,
/// which lists only active products and carries no provisioning field.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListCatalogProductsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An anonymous caller reads the catalogue.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListCatalog_ReturnsOk_WithoutAuthAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/public/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// The catalogue carries no provisioning package, server group or status, though the product
    /// has a package and a server group, and never lists an inactive product of the same group.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListCatalog_CarriesNoProvisioningFieldAndNoInactiveProductAsync()
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        var activeId = await ProductsTestSeeds.CreateProductAsync(admin);
        var package = $"pkg-{Guid.NewGuid():N}";
        int groupId;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            groupId = await db.Products.Where(p => p.Id == activeId).Select(p => p.GroupId).SingleAsync();
            await db.Products
                .Where(p => p.Id == activeId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.PackageName, package));
        }

        var created = await admin.PostAsJsonAsync("/api/products", new
        {
            groupId,
            name = "Retired",
            type = ProductType.SharedHosting,
            monthlyPrice = 1m,
            annualPrice = 10m,
        });
        var inactiveId = await created.Content.ReadFromJsonAsync<int>();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Products
                .Where(p => p.Id == inactiveId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, ProductStatus.Inactive));
        }

        var json = await factory.CreateClient().GetStringAsync($"/api/public/products?groupId={groupId}");

        json.Should().NotContain(package);
        foreach (var field in new[] { "packageName", "serverGroupId", "status" })
        {
            json.Should().NotContain($"\"{field}\"");
        }

        using var body = JsonDocument.Parse(json);
        body.RootElement.EnumerateArray().Select(p => p.GetProperty("id").GetInt32())
            .Should().Contain(activeId).And.NotContain(inactiveId);
    }

    /// <summary>A non-positive group filter is refused by the validator with 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListCatalog_NonPositiveGroupId_Returns400Async()
    {
        var response = await factory.CreateClient().GetAsync("/api/public/products?groupId=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A product filter lists that one product and no other, in any group.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListCatalog_ProductId_ReturnsOnlyThatProductAsync()
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        var wantedId = await ProductsTestSeeds.CreateProductAsync(admin);
        var otherId = await ProductsTestSeeds.CreateProductAsync(admin);

        using var body = JsonDocument.Parse(await factory.CreateClient().GetStringAsync($"/api/public/products?productId={wantedId}"));

        body.RootElement.EnumerateArray().Select(p => p.GetProperty("id").GetInt32())
            .Should().ContainSingle().Which.Should().Be(wantedId);
        otherId.Should().NotBe(wantedId);
    }

    /// <summary>A several-group filter lists the products of exactly those groups.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListCatalog_GroupIds_ReturnsTheProductsOfThoseGroupsOnlyAsync()
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        var firstId = await ProductsTestSeeds.CreateProductAsync(admin);
        var secondId = await ProductsTestSeeds.CreateProductAsync(admin);
        var outsideId = await ProductsTestSeeds.CreateProductAsync(admin);
        int firstGroup;
        int secondGroup;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            firstGroup = await db.Products.Where(p => p.Id == firstId).Select(p => p.GroupId).SingleAsync();
            secondGroup = await db.Products.Where(p => p.Id == secondId).Select(p => p.GroupId).SingleAsync();
        }

        using var body = JsonDocument.Parse(
            await factory.CreateClient().GetStringAsync($"/api/public/products?groupIds={firstGroup}&groupIds={secondGroup}"));

        var ids = body.RootElement.EnumerateArray().Select(p => p.GetProperty("id").GetInt32()).ToList();
        ids.Should().Contain([firstId, secondId]).And.NotContain(outsideId);
    }

    /// <summary>A non-positive product id or a non-positive group in the list is refused with 400.</summary>
    /// <param name="query">The query string under test.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("productId=0")]
    [InlineData("groupIds=1&groupIds=0")]
    public async Task ListCatalog_RefusedFilter_Returns400Async(string query)
    {
        var response = await factory.CreateClient().GetAsync($"/api/public/products?{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A list of more groups than a store has is refused with 400, so the filter cannot grow an unbounded SQL list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListCatalog_TooManyGroupIds_Returns400Async()
    {
        var query = string.Join('&', Enumerable.Range(1, 51).Select(id => $"groupIds={id}"));

        var response = await factory.CreateClient().GetAsync($"/api/public/products?{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
