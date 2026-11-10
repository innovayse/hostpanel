using System.Net;

namespace Innovayse.Hostpanel.Integration.Tests.Products;

/// <summary>
/// <c>GET /api/product-features</c> against Postgres: the public happy path.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetProductFeaturesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>GET is public: the storefront's comparison table reads it without a session.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ReturnsOk_WithoutAuth()
    {
        var response = await factory.CreateClient().GetAsync("/api/product-features");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
