using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>GET /api/knowledgebase/categories</c>: an anonymous caller gets each category of the published articles with
/// its published article count, and nothing else.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListPublishedKbCategoriesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// Published articles are counted per category, a draft is not counted, a category holding only a draft does not
    /// appear, and no entry carries a field beyond the name and the count.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublicRoute_CountsPublishedArticlesPerCategoryAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var counted = $"Counted-{Guid.NewGuid():N}";
        var draftsOnly = $"DraftsOnly-{Guid.NewGuid():N}";

        await CreateArticleAsync(admin, counted, publish: true);
        await CreateArticleAsync(admin, counted, publish: true);
        await CreateArticleAsync(admin, counted, publish: false);
        await CreateArticleAsync(admin, draftsOnly, publish: false);

        var response = await factory.CreateClient().GetAsync("/api/knowledgebase/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var categories = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        var entry = categories!.Should().ContainSingle(c => c.GetProperty("name").GetString() == counted).Subject;
        entry.GetProperty("articleCount").GetInt32().Should().Be(2);
        entry.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("name", "articleCount");
        categories.Should().NotContain(c => c.GetProperty("name").GetString() == draftsOnly);
    }

    /// <summary>Category names that differ only by letter case are one category with the articles of both.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CategoriesDifferingOnlyByCase_AreGroupedTogetherAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var stem = $"Mixed{Guid.NewGuid():N}";

        await CreateArticleAsync(admin, stem, publish: true);
        await CreateArticleAsync(admin, stem.ToUpperInvariant(), publish: true);
        await CreateArticleAsync(admin, stem.ToLowerInvariant(), publish: true);

        var response = await factory.CreateClient().GetAsync("/api/knowledgebase/categories");

        var categories = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        var entry = categories!.Should()
            .ContainSingle(c => string.Equals(c.GetProperty("name").GetString(), stem, StringComparison.OrdinalIgnoreCase)).Subject;
        entry.GetProperty("articleCount").GetInt32().Should().Be(3);
    }

    /// <summary>Creates an article in <paramref name="category"/> through the admin API and publishes it when asked.</summary>
    /// <param name="admin">An admin-authenticated client.</param>
    /// <param name="category">The category the article is filed under.</param>
    /// <param name="publish">Whether to publish the article after creating it.</param>
    /// <returns>A task that completes once the article exists in the requested state.</returns>
    private static async Task CreateArticleAsync(HttpClient admin, string category, bool publish)
    {
        var created = await admin.PostAsJsonAsync("/api/admin/knowledgebase", new
        {
            title = $"Article-{Guid.NewGuid():N}",
            content = "Body.",
            category,
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        if (publish)
        {
            var id = int.Parse((await created.Content.ReadAsStringAsync()).Trim());
            (await admin.PutAsync($"/api/admin/knowledgebase/{id}/publish", content: null))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
    }
}
