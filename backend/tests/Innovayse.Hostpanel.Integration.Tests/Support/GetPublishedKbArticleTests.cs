using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// Integration tests for the <c>GetPublishedKbArticle</c> use case: <c>GET /api/knowledgebase/{id}</c>. It is public,
/// answers in the reader's language, and treats a draft like a missing article. The by-slug route shares the segment;
/// <c>GetPublishedKbArticleBySlugTests</c> pins down which action takes which text.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetPublishedKbArticleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The public route returns a published article by id, without admin-only members.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublicRoute_ReturnsPublishedArticleAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryName = $"Cat-{Guid.NewGuid():N}";
        var categoryId = await KbTestSeeds.CreateCategoryAsync(admin, categoryName);
        var id = await KbTestSeeds.CreateArticleAsync(admin, categoryId, $"Published {Guid.NewGuid():N}", "Visible to everyone.", publish: true);

        var response = await factory.CreateClient().GetAsync($"/api/knowledgebase/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var article = await response.Content.ReadFromJsonAsync<JsonElement>();
        article.TryGetProperty("isPublished", out _).Should().BeFalse();
        article.GetProperty("content").GetString().Should().Be("Visible to everyone.");
        article.GetProperty("category").GetString().Should().Be(categoryName);
        article.GetProperty("slug").GetString().Should().StartWith("published-");
        article.GetProperty("categorySlug").GetString().Should().StartWith("cat-");
    }

    /// <summary>The article comes back in the requested language, else the default, else the first it has.</summary>
    /// <param name="language">The <c>X-Language</c> sent, or <see langword="null"/> for none.</param>
    /// <param name="expectedTitle">The title expected.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("hy", "Հայերեն վերնագիր")]
    [InlineData("ru", "English title")]
    [InlineData(null, "English title")]
    public async Task PublicRoute_AnswersInTheReadersLanguageAsync(string? language, string expectedTitle)
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var id = await KbTestSeeds.CreateArticleAsync(admin, KbTestSeeds.ArticleBody(
            await KbTestSeeds.CreateCategoryAsync(admin),
            [("en", "English title", "English body."), ("hy", "Հայերեն վերնագիր", "Հայերեն տեքստ.")],
            isPublished: true));

        var byId = await StorefrontContentTestSeeds.GetPublicAsync(factory, $"/api/knowledgebase/{id}", language);

        byId.StatusCode.Should().Be(HttpStatusCode.OK);
        (await byId.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString().Should().Be(expectedTitle);
    }

    /// <summary>A missing article and an unpublished draft are refused alike.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublicRoute_RefusesMissingAndUnpublishedAlikeAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var anonymous = factory.CreateClient();
        var marker = Guid.NewGuid().ToString("N")[..10];
        var draftId = await KbTestSeeds.CreateArticleAsync(admin, await KbTestSeeds.CreateCategoryAsync(admin), $"Draft {marker}", "Not yet public.");

        var draftById = await anonymous.GetAsync($"/api/knowledgebase/{draftId}");
        var missingById = await anonymous.GetAsync($"/api/knowledgebase/{SupportTestSeeds.NoSuchId}");

        foreach (var response in new[] { draftById, missingById })
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.KBARTICLE_NOT_FOUND);
        }
    }
}
