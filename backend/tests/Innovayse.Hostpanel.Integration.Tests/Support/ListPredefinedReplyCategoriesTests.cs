using System.Net;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>GET /api/predefined-replies/categories</c>: an admin can list predefined reply categories
/// with their reply counts, and the list is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListPredefinedReplyCategoriesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can list predefined reply categories.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanListCategoriesAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        await SupportTestSeeds.CreatePredefinedReplyCategoryAsync(client);

        var response = await client.GetAsync("/api/predefined-replies/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>The category list is closed to a caller with no credential.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListIsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/predefined-replies/categories");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
