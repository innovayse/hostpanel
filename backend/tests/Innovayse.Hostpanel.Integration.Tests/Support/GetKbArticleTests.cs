using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>GET /api/admin/knowledgebase/{id}</c>: an admin can read a single knowledge base article,
/// published or not, an id names no article is refused <c>KBARTICLE_NOT_FOUND</c>, and the action
/// is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetKbArticleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can read a knowledge base article that has not yet been published.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanReadAnUnpublishedKbArticleAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryName = $"Cat-{Guid.NewGuid():N}";
        var categoryId = await KbTestSeeds.CreateCategoryAsync(client, categoryName);
        var id = await KbTestSeeds.CreateArticleAsync(client, categoryId, "Readable", "Some body.");

        var response = await client.GetAsync($"/api/admin/knowledgebase/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var article = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        article.GetProperty("id").GetInt32().Should().Be(id);
        article.GetProperty("slug").GetString().Should().StartWith("readable");
        article.GetProperty("categoryId").GetInt32().Should().Be(categoryId);
        article.GetProperty("categoryName").GetString().Should().Be(categoryName);
        article.GetProperty("isPublished").GetBoolean().Should().BeFalse();
        article.GetProperty("translations").GetProperty("en").GetProperty("content").GetString().Should().Be("Some body.");
    }

    /// <summary>Reading a knowledge base article that does not exist answers 404 with <c>KBARTICLE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetUnknownKbArticleAnswersNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync($"/api/admin/knowledgebase/{SupportTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.KBARTICLE_NOT_FOUND);
    }

    /// <summary>A caller with no credential cannot read the admin knowledge base article route.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync($"/api/admin/knowledgebase/{SupportTestSeeds.NoSuchId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
