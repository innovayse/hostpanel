using System.Net;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>GET /api/products</c> against Postgres: the staff view of the products, which carries the
/// provisioning fields and is therefore closed to anyone but staff.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetProductsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin reads the staff view, inactive and unsellable products included.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetProductsAsync_ReturnsOk_ForAnAdminAsync()
    {
        var admin = await ProductsTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/products?activeOnly=false&includeUnsellable=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>An anonymous caller is refused with 401: the storefront reads <c>/api/public/products</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetProductsAsync_Returns401_WithoutAuthAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
