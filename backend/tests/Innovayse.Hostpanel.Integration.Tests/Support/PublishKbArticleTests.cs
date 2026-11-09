using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>PUT /api/admin/knowledgebase/{id}/publish</c>: an admin can publish a knowledge base
/// article, an id names no article is refused <c>KBARTICLE_NOT_FOUND</c>, and the action is
/// closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PublishKbArticleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can publish a knowledge base article, after which it reads back as published.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanPublishAKbArticleAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var createResponse = await client.PostAsJsonAsync("/api/admin/knowledgebase", new
        {
            title = $"Article-{Guid.NewGuid():N}",
            content = "Body text.",
            category = "General",
        });
        var id = int.Parse((await createResponse.Content.ReadAsStringAsync()).Trim());

        var response = await client.PutAsync($"/api/admin/knowledgebase/{id}/publish", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Publishing a knowledge base article that does not exist answers 404 with <c>KBARTICLE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublishUnknownKbArticleAnswersNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsync($"/api/admin/knowledgebase/{SupportTestSeeds.NoSuchId}/publish", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.KBARTICLE_NOT_FOUND);
    }

    /// <summary>A caller with no credential cannot publish a knowledge base article.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PutAsync(
            $"/api/admin/knowledgebase/{SupportTestSeeds.NoSuchId}/publish", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
