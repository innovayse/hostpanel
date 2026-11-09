using System.Net;
using System.Net.Http.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>POST /api/announcements</c>: an admin can create an announcement, and the action is closed
/// to a caller with no credential.
/// </summary>
/// <remarks>
/// Runs in CI, where Testcontainers can start PostgreSQL; on a laptop without a reachable docker
/// socket every test in this project fails before the API boots, and that is the environment, not
/// these tests.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateAnnouncementTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can create an announcement.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanCreateAnAnnouncementAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/announcements", new
        {
            title = $"Announcement-{Guid.NewGuid():N}",
            content = "Something happened.",
            isPublished = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>A caller with no credential cannot create an announcement.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/announcements", new
        {
            title = "Anything",
            content = "Anything.",
            isPublished = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
