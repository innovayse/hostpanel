using System.Net;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>GET /api/downloads/categories</c>: an admin can list download categories, and the list is
/// closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListDownloadCategoriesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can list download categories.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanListDownloadCategoriesAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        await SupportTestSeeds.CreateDownloadCategoryAsync(client);

        var response = await client.GetAsync("/api/downloads/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>The download category list is closed to a caller with no credential.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListIsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/downloads/categories");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
