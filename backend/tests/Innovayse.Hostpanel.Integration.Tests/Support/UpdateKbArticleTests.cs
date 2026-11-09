using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>PUT /api/admin/knowledgebase/{id}</c>: an admin can update a knowledge base article, an id
/// names no article is refused <c>KBARTICLE_NOT_FOUND</c>, and the action is closed to a caller
/// with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateKbArticleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can update an existing knowledge base article.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanUpdateAKbArticleAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var createResponse = await client.PostAsJsonAsync("/api/admin/knowledgebase", new
        {
            title = $"Article-{Guid.NewGuid():N}",
            content = "Body text.",
            category = "General",
        });
        var id = int.Parse((await createResponse.Content.ReadAsStringAsync()).Trim());

        var response = await client.PutAsJsonAsync($"/api/admin/knowledgebase/{id}", new
        {
            title = "Renamed",
            content = "Updated body.",
            category = "General",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Updating a knowledge base article that does not exist answers 404 with <c>KBARTICLE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateUnknownKbArticleAnswersNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"/api/admin/knowledgebase/{SupportTestSeeds.NoSuchId}", new
        {
            title = "Title",
            content = "Content",
            category = "General",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.KBARTICLE_NOT_FOUND);
    }

    /// <summary>A caller with no credential cannot update a knowledge base article.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PutAsJsonAsync(
            $"/api/admin/knowledgebase/{SupportTestSeeds.NoSuchId}", new
            {
                title = "Title",
                content = "Content",
                category = "General",
            });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
