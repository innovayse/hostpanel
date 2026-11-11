using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// Integration tests for the <c>ListPublishedKbCategories</c> use case: <c>GET /api/knowledgebase/categories</c>.
/// It is public and lists the categories that hold published articles, with their slug and count.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListPublishedKbCategoriesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The list counts published articles per category, carries the slug, and leaves out a category with drafts only.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublicRoute_CountsPublishedArticlesPerCategoryAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var counted = $"Counted-{Guid.NewGuid():N}";
        var draftsOnly = $"DraftsOnly-{Guid.NewGuid():N}";
        var countedId = await KbTestSeeds.CreateCategoryAsync(admin, counted);
        var draftsId = await KbTestSeeds.CreateCategoryAsync(admin, draftsOnly);
        await KbTestSeeds.CreateArticleAsync(admin, countedId, publish: true);
        await KbTestSeeds.CreateArticleAsync(admin, countedId, publish: true);
        await KbTestSeeds.CreateArticleAsync(admin, countedId);
        await KbTestSeeds.CreateArticleAsync(admin, draftsId);

        var response = await factory.CreateClient().GetAsync("/api/knowledgebase/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var categories = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        var entry = categories!.Should().ContainSingle(c => c.GetProperty("name").GetString() == counted).Subject;
        entry.GetProperty("articleCount").GetInt32().Should().Be(2);
        entry.GetProperty("slug").GetString().Should().Be(counted.ToLowerInvariant());
        entry.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("name", "articleCount", "slug");
        categories.Should().NotContain(c => c.GetProperty("name").GetString() == draftsOnly);
    }

    /// <summary>Two categories whose names differ only by case are two categories, each with its own slug.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CategoriesDifferingOnlyByCase_AreTwoCategoriesWithTheirOwnSlugsAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var stem = $"Mixed{Guid.NewGuid():N}";
        var upper = await KbTestSeeds.CreateCategoryAsync(admin, stem.ToUpperInvariant());
        var lower = await KbTestSeeds.CreateCategoryAsync(admin, stem.ToLowerInvariant());
        await KbTestSeeds.CreateArticleAsync(admin, upper, publish: true);
        await KbTestSeeds.CreateArticleAsync(admin, lower, publish: true);

        var response = await factory.CreateClient().GetAsync("/api/knowledgebase/categories");

        var categories = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        var matching = categories!.Where(c => string.Equals(c.GetProperty("name").GetString(), stem, StringComparison.OrdinalIgnoreCase)).ToArray();
        matching.Should().HaveCount(2);
        matching.Select(c => c.GetProperty("slug").GetString()).Should().OnlyHaveUniqueItems();
    }
}
