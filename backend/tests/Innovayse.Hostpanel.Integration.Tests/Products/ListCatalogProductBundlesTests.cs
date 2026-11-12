using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>GET /api/public/product-bundles</c> against Postgres: the storefront's bundle list anyone may
/// read, which lists only active bundles and carries no staff field.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListCatalogProductBundlesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An anonymous caller reads the list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListCatalog_ReturnsOk_WithoutAuth()
    {
        var response = await factory.CreateClient().GetAsync("/api/public/product-bundles");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>An inactive bundle is never listed, an active one is, and no row carries a staff field.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListCatalog_ReturnsOnlyActiveBundlesWithoutStaffFields()
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        var product = await ProductsTestSeeds.CreateProductAsync(admin);
        var activeId = await ProductsTestSeeds.CreateBundleAsync(admin, ProductsTestSeeds.BundleBody(product));
        var inactiveId = await ProductsTestSeeds.CreateBundleAsync(admin, ProductsTestSeeds.BundleBody(product));
        var off = ProductsTestSeeds.BundleBody(product);
        off["isActive"] = false;
        (await admin.PutAsJsonAsync($"/api/products/bundles/{inactiveId}", off)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await factory.CreateClient().GetFromJsonAsync<JsonElement[]>("/api/public/product-bundles");

        list!.Should().Contain(b => b.GetProperty("id").GetInt32() == activeId);
        list.Should().NotContain(b => b.GetProperty("id").GetInt32() == inactiveId);
        list.Should().AllSatisfy(b =>
        {
            b.TryGetProperty("isActive", out _).Should().BeFalse();
            b.TryGetProperty("sortOrder", out _).Should().BeFalse();
        });
    }

    /// <summary>A bundle with a product that is no longer on sale is not listed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListCatalog_HidesABundleWithAProductNotOnSale()
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        var product = await ProductsTestSeeds.CreateProductAsync(admin);
        var id = await ProductsTestSeeds.CreateBundleAsync(admin, ProductsTestSeeds.BundleBody(product));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Products
                .Where(p => p.Id == product)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, ProductStatus.Inactive));
        }

        var list = await factory.CreateClient().GetFromJsonAsync<JsonElement[]>("/api/public/product-bundles");

        list!.Should().NotContain(b => b.GetProperty("id").GetInt32() == id);
    }

    /// <summary>A row carries the offer: the domain, the discount and the components with their names.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListCatalog_CarriesTheOfferAndComponents()
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);
        var hosting = await ProductsTestSeeds.CreateProductAsync(admin);
        var extra = await ProductsTestSeeds.CreateProductAsync(admin);
        var id = await ProductsTestSeeds.CreateBundleAsync(admin, ProductsTestSeeds.BundleBody(hosting, extra));

        var list = await factory.CreateClient().GetFromJsonAsync<JsonElement[]>("/api/public/product-bundles");

        var bundle = list!.Single(b => b.GetProperty("id").GetInt32() == id);
        bundle.GetProperty("includesDomain").GetBoolean().Should().BeTrue();
        bundle.GetProperty("domainYears").GetInt32().Should().Be(1);
        bundle.GetProperty("discountType").GetString().Should().Be("Percentage");
        bundle.GetProperty("discountAmount").GetDecimal().Should().Be(10m);
        bundle.GetProperty("discountCurrency").ValueKind.Should().Be(JsonValueKind.Null);
        var components = bundle.GetProperty("components").EnumerateArray().ToList();
        components.Select(c => c.GetProperty("productId").GetInt32()).Should().Equal(hosting, extra);
        components[0].GetProperty("name").GetString().Should().Be("Featured");
        components[0].GetProperty("isPrimary").GetBoolean().Should().BeTrue();
    }
}
