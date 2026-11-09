using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>DELETE /api/admin/knowledgebase/{id}</c>: an admin can delete a knowledge base article, an
/// id names no article is refused <c>KBARTICLE_NOT_FOUND</c>, and the action is closed to a
/// caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteKbArticleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can delete a knowledge base article.</summary>
    [Fact]
    public async Task AdminCanDeleteAKbArticleAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var id = await KbTestSeeds.CreateArticleAsync(client, await KbTestSeeds.CreateCategoryAsync(client));

        var response = await client.DeleteAsync($"/api/admin/knowledgebase/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Deleting a knowledge base article that does not exist answers 404 with <c>KBARTICLE_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task DeleteUnknownKbArticleAnswersNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync($"/api/admin/knowledgebase/{SupportTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.KBARTICLE_NOT_FOUND);
    }

    /// <summary>A caller with no credential cannot delete a knowledge base article.</summary>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().DeleteAsync($"/api/admin/knowledgebase/{SupportTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
