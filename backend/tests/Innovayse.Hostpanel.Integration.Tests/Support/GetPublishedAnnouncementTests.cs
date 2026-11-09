using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>GET /api/public/announcements/{id}</c>: anyone reads a published announcement; a draft and an
/// unknown id both answer 404.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetPublishedAnnouncementTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Creates an announcement as an admin and returns its id.</summary>
    /// <param name="isPublished">Whether the announcement is published.</param>
    /// <returns>The new announcement's id.</returns>
    private async Task<int> CreateAsync(bool isPublished)
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var created = await admin.PostAsJsonAsync("/api/announcements", new
        {
            title = $"News-{Guid.NewGuid():N}",
            content = "Body.",
            isPublished,
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        return await created.Content.ReadFromJsonAsync<int>();
    }

    /// <summary>A published announcement is readable with no credential, in the public shape.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublishedAnnouncement_IsReadableAnonymouslyAsync()
    {
        var id = await CreateAsync(isPublished: true);

        var response = await factory.CreateClient().GetAsync($"/api/public/announcements/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("id").GetInt32().Should().Be(id);
        body.GetProperty("content").GetString().Should().Be("Body.");
        body.TryGetProperty("publishedAt", out _).Should().BeTrue();
        body.TryGetProperty("isPublished", out _).Should().BeFalse();
    }

    /// <summary>A draft answers 404, the same as an id that names nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Draft_AnswersNotFoundAsync()
    {
        var id = await CreateAsync(isPublished: false);

        var response = await factory.CreateClient().GetAsync($"/api/public/announcements/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>An unknown id answers 404.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownId_AnswersNotFoundAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/public/announcements/2000000000");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
