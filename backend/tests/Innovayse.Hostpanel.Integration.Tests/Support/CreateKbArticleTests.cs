using System.Net;
using System.Net.Http.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>POST /api/admin/knowledgebase</c>: an admin can create a knowledge base article
/// (unpublished by default), and the action is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateKbArticleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can create a knowledge base article.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanCreateAKbArticleAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/knowledgebase", new
        {
            title = $"Article-{Guid.NewGuid():N}",
            content = "Body text.",
            category = "General",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>An article created with <c>isPublished: true</c> reads back as published; without it, as a draft.</summary>
    /// <param name="isPublished">The flag sent in the body.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreatedArticleKeepsTheIsPublishedFlagAsync(bool isPublished)
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var createResponse = await client.PostAsJsonAsync("/api/admin/knowledgebase", new
        {
            title = $"Article-{Guid.NewGuid():N}",
            content = "Body text.",
            category = "General",
            isPublished,
        });
        var id = int.Parse((await createResponse.Content.ReadAsStringAsync()).Trim());

        var article = await client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/admin/knowledgebase/{id}");

        article.GetProperty("isPublished").GetBoolean().Should().Be(isPublished);
    }

    /// <summary>A caller with no credential cannot create a knowledge base article.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/admin/knowledgebase", new
        {
            title = "Anything",
            content = "Anything.",
            category = "General",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
