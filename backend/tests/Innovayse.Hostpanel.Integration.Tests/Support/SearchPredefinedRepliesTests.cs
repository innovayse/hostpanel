using System.Net;
using System.Net.Http.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>GET /api/predefined-replies/search</c>: an admin can search predefined replies by name or
/// content, an empty term is refused before any query runs, and the action is closed to a caller
/// with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SearchPredefinedRepliesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can search predefined replies and finds a match by content.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanSearchRepliesAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await SupportTestSeeds.CreatePredefinedReplyCategoryAsync(client);
        var uniqueWord = $"Zephyr{Guid.NewGuid():N}";
        await client.PostAsJsonAsync("/api/predefined-replies", new
        {
            name = $"Reply-{Guid.NewGuid():N}",
            content = $"Thanks for reaching out about {uniqueWord}.",
            categoryId,
        });

        var response = await client.GetAsync($"/api/predefined-replies/search?searchTerm={uniqueWord}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain(uniqueWord);
    }

    /// <summary>An empty search term is refused before any query runs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchRefusesAnEmptyTermAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/predefined-replies/search?searchTerm=");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The search route is closed to a caller with no credential.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/predefined-replies/search?searchTerm=anything");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
