using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>DELETE /api/products/bundles/{id}</c> against Postgres: the bundle and its components go, the
/// products and the orders placed from the bundle stay.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteProductBundleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Deleting a bundle removes it and its components but not its products, and clears the link of an order placed from it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_RemovesTheBundle_KeepsProductsAndOrders()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);
        var product = await ProductsTestSeeds.CreateProductAsync(client);
        var bundleId = await ProductsTestSeeds.CreateBundleAsync(client, ProductsTestSeeds.BundleBody(product));
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Orders.FirstAsync(o => o.Id == orderId)).PlaceFromBundle(bundleId);
            await db.SaveChangesAsync();
        }

        var response = await client.DeleteAsync($"/api/products/bundles/{bundleId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var check = factory.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<AppDbContext>();
        (await context.Set<ProductBundle>().AnyAsync(b => b.Id == bundleId)).Should().BeFalse();
        (await context.Set<ProductBundleItem>().AnyAsync(i => i.BundleId == bundleId)).Should().BeFalse();
        (await context.Products.AnyAsync(p => p.Id == product)).Should().BeTrue();
        (await context.Set<Order>().AsNoTracking().SingleAsync(o => o.Id == orderId)).BundleId.Should().BeNull();
    }

    /// <summary>Deleting an unknown bundle answers 404 with <c>PRODUCT_BUNDLE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_UnknownId_RefusesNotFound()
    {
        var client = await ProductsTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync("/api/products/bundles/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ProductsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_BUNDLE_NOT_FOUND);
    }

    /// <summary>DELETE /api/products/bundles/{id} without auth returns 401 Unauthorized.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_Returns401_WithoutAuth()
    {
        var response = await factory.CreateClient().DeleteAsync("/api/products/bundles/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
