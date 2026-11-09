using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>GET /api/announcements/{id}</c>: an admin can read a single announcement (draft or
/// published), an id names no announcement is refused <c>ANNOUNCEMENT_NOT_FOUND</c>, and the
/// action is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetAnnouncementTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can read back an announcement by id.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanReadAnAnnouncementAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var createResponse = await client.PostAsJsonAsync("/api/announcements", new
        {
            title = $"Announcement-{Guid.NewGuid():N}",
            content = "Something happened.",
            isPublished = true,
        });
        var id = int.Parse((await createResponse.Content.ReadAsStringAsync()).Trim());

        var response = await client.GetAsync($"/api/announcements/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Reading an announcement that does not exist answers 404 with <c>ANNOUNCEMENT_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetUnknownAnnouncementAnswersNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync($"/api/announcements/{SupportTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ANNOUNCEMENT_NOT_FOUND);
    }

    /// <summary>A caller with no credential cannot read an announcement.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync($"/api/announcements/{SupportTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
