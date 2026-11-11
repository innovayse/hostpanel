using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// Integration tests for the <c>CreateKbArticle</c> use case: <c>POST /api/admin/knowledgebase</c>. The article
/// takes a category by id and its title and body as translations; its slug comes from the English title.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateKbArticleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can create an article, and the answer is 201.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanCreateAKbArticleAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await KbTestSeeds.CreateCategoryAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/admin/knowledgebase", KbTestSeeds.ArticleBody(categoryId, [("en", $"Article-{Guid.NewGuid():N}", "Body text.")]));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>The published flag sent on creation is kept.</summary>
    /// <param name="isPublished">The flag sent.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreatedArticleKeepsTheIsPublishedFlagAsync(bool isPublished)
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await KbTestSeeds.CreateCategoryAsync(client);

        var id = await KbTestSeeds.CreateArticleAsync(
            client, KbTestSeeds.ArticleBody(categoryId, [("en", $"Article-{Guid.NewGuid():N}", "Body text.")], isPublished));

        (await KbTestSeeds.GetArticleAsync(client, id)).GetProperty("isPublished").GetBoolean().Should().Be(isPublished);
    }

    /// <summary>The slug is built from the English title, and the article keeps every translation it was sent.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SlugComesFromTheEnglishTitle_AndEveryTranslationIsKeptAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await KbTestSeeds.CreateCategoryAsync(client);
        var marker = Guid.NewGuid().ToString("N")[..8];

        var id = await KbTestSeeds.CreateArticleAsync(client, KbTestSeeds.ArticleBody(
            categoryId,
            [("en", $"Getting Started: SSL {marker}!", "English."), ("ru", "Начало работы", "Русский."), ("hy", "Սկիզբ", "Հայերեն.")]));

        var article = await KbTestSeeds.GetArticleAsync(client, id);
        article.GetProperty("slug").GetString().Should().Be($"getting-started-ssl-{marker}");
        article.GetProperty("categoryId").GetInt32().Should().Be(categoryId);
        article.GetProperty("translations").EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("en", "ru", "hy");
        article.GetProperty("translations").GetProperty("ru").GetProperty("title").GetString().Should().Be("Начало работы");
    }

    /// <summary>Two articles with the same title get different slugs, the second numbered.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TwoArticlesWithTheSameTitle_GetDifferentSlugsAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await KbTestSeeds.CreateCategoryAsync(client);
        var title = $"Same title {Guid.NewGuid():N}";

        var first = await KbTestSeeds.CreateArticleAsync(client, categoryId, title);
        var second = await KbTestSeeds.CreateArticleAsync(client, categoryId, title);
        var third = await KbTestSeeds.CreateArticleAsync(client, categoryId, title.ToUpperInvariant());

        var slugs = new List<string>();
        foreach (var id in new[] { first, second, third })
        {
            slugs.Add((await KbTestSeeds.GetArticleAsync(client, id)).GetProperty("slug").GetString()!);
        }

        slugs.Should().Equal(slugs[0], $"{slugs[0]}-2", $"{slugs[0]}-3");
    }

    /// <summary>A title that leaves no Latin letters, a number and a reserved word each get a usable slug.</summary>
    /// <param name="title">The English title.</param>
    /// <param name="expectedSlug">What the slug starts with.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("2024", "article-2024")]
    [InlineData("Привет", "article")]
    [InlineData("Articles", "articles-2")]
    [InlineData("categories", "categories-2")]
    public async Task TitleWithNoUsableSlug_GetsAFallbackAsync(string title, string expectedSlug)
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await KbTestSeeds.CreateCategoryAsync(client);

        var id = await KbTestSeeds.CreateArticleAsync(client, categoryId, title);

        var slug = (await KbTestSeeds.GetArticleAsync(client, id)).GetProperty("slug").GetString()!;
        slug.Should().StartWith(expectedSlug);
        slug.Should().MatchRegex("^[a-z0-9]+(-[a-z0-9]+)*$").And.NotMatchRegex("^[0-9]+$");
        new[] { "articles", "categories" }.Should().NotContain(slug);
    }

    /// <summary>A category that does not exist is refused with 404.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownCategory_AnswersNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/admin/knowledgebase", KbTestSeeds.ArticleBody(SupportTestSeeds.NoSuchId, [("en", "Title", "Body.")]));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.KBCATEGORY_NOT_FOUND);
    }

    /// <summary>An article without the default language, with an unsupported language, or with a blank part is refused with 400.</summary>
    /// <param name="locale">The locale sent.</param>
    /// <param name="title">The title sent.</param>
    /// <param name="content">The body sent.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("ru", "Только русский", "Текст.")]
    [InlineData("de", "Nur Deutsch", "Text.")]
    [InlineData("en", "", "Body.")]
    [InlineData("en", "Title", "")]
    public async Task InvalidTranslations_AreRefusedAsync(string locale, string title, string content)
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await KbTestSeeds.CreateCategoryAsync(client);

        var response = await client.PostAsJsonAsync("/api/admin/knowledgebase", KbTestSeeds.ArticleBody(categoryId, [(locale, title, content)]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A title longer than the column is refused with 400, not with a database error.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TitleLongerThanTheLimit_IsRefusedAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await KbTestSeeds.CreateCategoryAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/admin/knowledgebase", KbTestSeeds.ArticleBody(categoryId, [("en", new string('a', 501), "Body.")]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>An anonymous caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/admin/knowledgebase", KbTestSeeds.ArticleBody(1, [("en", "Anything", "Anything.")]));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
