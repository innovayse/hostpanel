using System.Net;
using System.Net.Http.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>GET /api/announcements/published</c>: a signed-in member reads the published announcements
/// feed with drafts excluded, and the action is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListPublishedAnnouncementsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A signed-in member reads the published announcements feed, drafts excluded.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublishedAnnouncementsFeedExcludesDraftsAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);

        var publishedTitle = $"Published-{Guid.NewGuid():N}";
        var draftTitle = $"Draft-{Guid.NewGuid():N}";

        (await admin.PostAsJsonAsync("/api/announcements", new
        {
            title = publishedTitle,
            content = "Visible.",
            isPublished = true,
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        (await admin.PostAsJsonAsync("/api/announcements", new
        {
            title = draftTitle,
            content = "Not yet.",
            isPublished = false,
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        var feedResponse = await admin.GetAsync("/api/announcements/published?page=1&pageSize=100");
        feedResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await feedResponse.Content.ReadAsStringAsync();
        body.Should().Contain(publishedTitle);
        body.Should().NotContain(draftTitle);
    }

    /// <summary>A caller with no credential cannot read the published announcements feed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/announcements/published?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
