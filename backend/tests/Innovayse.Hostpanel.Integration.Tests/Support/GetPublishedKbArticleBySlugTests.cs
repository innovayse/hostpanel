using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// Integration tests for the <c>GetPublishedKbArticleBySlug</c> use case: <c>GET /api/knowledgebase/{slug}</c>. It is
/// public, matches the slug without regard to case, answers in the reader's language, and treats a draft like a
/// missing article. The route shares its segment with <c>{id:int}</c> and with the literal <c>articles</c> and
/// <c>categories</c>, so the tests pin down which action takes which text.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetPublishedKbArticleBySlugTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The public route returns the article by slug, and a slug typed in capitals finds it too, answered with the canonical one.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReturnsTheArticleBySlug_InAnyLetterCaseAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var marker = Guid.NewGuid().ToString("N")[..10];
        var id = await KbTestSeeds.CreateArticleAsync(
            admin, await KbTestSeeds.CreateCategoryAsync(admin), $"By Slug {marker}", "Found by slug.", publish: true);

        var lower = await factory.CreateClient().GetAsync($"/api/knowledgebase/by-slug-{marker}");
        var upper = await factory.CreateClient().GetAsync($"/api/knowledgebase/BY-Slug-{marker.ToUpperInvariant()}");

        lower.StatusCode.Should().Be(HttpStatusCode.OK);
        upper.StatusCode.Should().Be(HttpStatusCode.OK);
        var fromLower = await lower.Content.ReadFromJsonAsync<JsonElement>();
        var fromUpper = await upper.Content.ReadFromJsonAsync<JsonElement>();
        fromLower.GetProperty("id").GetInt32().Should().Be(id);
        fromUpper.GetProperty("id").GetInt32().Should().Be(id);
        fromUpper.GetProperty("slug").GetString().Should().Be($"by-slug-{marker}");
    }

    /// <summary>The article comes back in the requested language, else the default, else the first it has.</summary>
    /// <param name="language">The <c>X-Language</c> sent, or <see langword="null"/> for none.</param>
    /// <param name="expectedTitle">The title expected.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("ru", "Русский заголовок")]
    [InlineData("hy", "English title")]
    [InlineData(null, "English title")]
    public async Task AnswersInTheReadersLanguageAsync(string? language, string expectedTitle)
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var marker = Guid.NewGuid().ToString("N")[..10];
        await KbTestSeeds.CreateArticleAsync(admin, KbTestSeeds.ArticleBody(
            await KbTestSeeds.CreateCategoryAsync(admin),
            [("en", $"English title {marker}", "English body."), ("ru", "Русский заголовок", "Русский текст.")],
            isPublished: true));

        var response = await StorefrontContentTestSeeds.GetPublicAsync(factory, $"/api/knowledgebase/english-title-{marker}", language);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var title = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString();
        title.Should().StartWith(expectedTitle);
    }

    /// <summary>A missing article and an unpublished draft are refused alike.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesMissingAndUnpublishedAlikeAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var anonymous = factory.CreateClient();
        var marker = Guid.NewGuid().ToString("N")[..10];
        await KbTestSeeds.CreateArticleAsync(admin, await KbTestSeeds.CreateCategoryAsync(admin), $"Draft {marker}", "Not yet public.");

        var draft = await anonymous.GetAsync($"/api/knowledgebase/draft-{marker}");
        var missing = await anonymous.GetAsync("/api/knowledgebase/no-such-article-anywhere");

        foreach (var response in new[] { draft, missing })
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.KBARTICLE_NOT_FOUND);
        }
    }

    /// <summary>
    /// The shared segment is read the one right way: the literal words keep their own actions in any letter case, a
    /// number too large for an id names no article, and text that is not a slug matches no route.
    /// </summary>
    /// <param name="path">The path after <c>/api/knowledgebase/</c>.</param>
    /// <param name="status">The status expected.</param>
    /// <param name="isList">Whether the body is the articles page (an object with items).</param>
    /// <param name="isArray">Whether the body is the categories array.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("articles", HttpStatusCode.OK, true, false)]
    [InlineData("Articles", HttpStatusCode.OK, true, false)]
    [InlineData("ARTICLES", HttpStatusCode.OK, true, false)]
    [InlineData("categories", HttpStatusCode.OK, false, true)]
    [InlineData("Categories", HttpStatusCode.OK, false, true)]
    [InlineData("99999999999", HttpStatusCode.NotFound, false, false)]
    [InlineData("0", HttpStatusCode.BadRequest, false, false)]
    [InlineData("bad_slug", HttpStatusCode.NotFound, false, false)]
    [InlineData("-leading-hyphen", HttpStatusCode.NotFound, false, false)]
    [InlineData("trailing-hyphen-", HttpStatusCode.NotFound, false, false)]
    [InlineData("double--hyphen", HttpStatusCode.NotFound, false, false)]
    [InlineData("a/b", HttpStatusCode.NotFound, false, false)]
    [InlineData("articles/extra", HttpStatusCode.NotFound, false, false)]
    public async Task SharedSegment_IsReadTheOneRightWayAsync(string path, HttpStatusCode status, bool isList, bool isArray)
    {
        var response = await factory.CreateClient().GetAsync($"/api/knowledgebase/{path}");

        response.StatusCode.Should().Be(status);
        if (isList || isArray)
        {
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            body.ValueKind.Should().Be(isArray ? JsonValueKind.Array : JsonValueKind.Object);
            if (isList)
            {
                body.TryGetProperty("items", out _).Should().BeTrue("the literal 'articles' is the list, not an article called articles");
            }
        }
    }
}
