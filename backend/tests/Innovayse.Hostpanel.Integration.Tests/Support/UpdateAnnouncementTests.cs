using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>PUT /api/announcements/{id}</c>: an admin can update an announcement, an id names no
/// announcement is refused <c>ANNOUNCEMENT_NOT_FOUND</c>, and the action is closed to a caller
/// with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateAnnouncementTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can update an existing announcement.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanUpdateAnAnnouncementAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var createResponse = await client.PostAsJsonAsync("/api/announcements", new
        {
            title = $"Announcement-{Guid.NewGuid():N}",
            content = "Original.",
            isPublished = false,
        });
        var id = int.Parse((await createResponse.Content.ReadAsStringAsync()).Trim());

        var response = await client.PutAsJsonAsync($"/api/announcements/{id}", new
        {
            title = "Renamed",
            content = "Updated.",
            isPublished = true,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Updating an announcement that does not exist answers 404 with <c>ANNOUNCEMENT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateUnknownAnnouncementAnswersNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"/api/announcements/{SupportTestSeeds.NoSuchId}", new
        {
            title = "Anything",
            content = "Anything.",
            isPublished = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ANNOUNCEMENT_NOT_FOUND);
    }

    /// <summary>A caller with no credential cannot update an announcement.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PutAsJsonAsync($"/api/announcements/{SupportTestSeeds.NoSuchId}", new
        {
            title = "Anything",
            content = "Anything.",
            isPublished = false,
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
