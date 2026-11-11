using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// Integration tests for the <c>UpdateKbArticle</c> use case: <c>PUT /api/admin/knowledgebase/{id}</c>. The category
/// and the translations are replaced; the slug never changes.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateKbArticleTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can update an article: the text is replaced, a locale left out is removed, the slug stays.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanUpdateAKbArticle_KeepingItsSlugAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await KbTestSeeds.CreateCategoryAsync(client);
        var id = await KbTestSeeds.CreateArticleAsync(client, KbTestSeeds.ArticleBody(
            categoryId, [("en", $"Original {Guid.NewGuid():N}", "Old body."), ("ru", "Старый", "Старый текст.")]));
        var slug = (await KbTestSeeds.GetArticleAsync(client, id)).GetProperty("slug").GetString();

        var response = await client.PutAsJsonAsync(
            $"/api/admin/knowledgebase/{id}", KbTestSeeds.ArticleBody(categoryId, [("en", "Renamed", "Updated body."), ("hy", "Նոր", "Նոր տեքստ.")]));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var article = await KbTestSeeds.GetArticleAsync(client, id);
        article.GetProperty("slug").GetString().Should().Be(slug, "links to the article keep working when it is renamed");
        var translations = article.GetProperty("translations");
        translations.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("en", "hy");
        translations.GetProperty("en").GetProperty("title").GetString().Should().Be("Renamed");
    }

    /// <summary>An article can be moved to another category.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanMoveAnArticleToAnotherCategoryAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var from = await KbTestSeeds.CreateCategoryAsync(client);
        var to = await KbTestSeeds.CreateCategoryAsync(client);
        var id = await KbTestSeeds.CreateArticleAsync(client, from);

        var response = await client.PutAsJsonAsync($"/api/admin/knowledgebase/{id}", KbTestSeeds.ArticleBody(to, [("en", "Moved", "Body.")]));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await KbTestSeeds.GetArticleAsync(client, id)).GetProperty("categoryId").GetInt32().Should().Be(to);
    }

    /// <summary>Updating an article that does not exist answers 404.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateUnknownKbArticleAnswersNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await KbTestSeeds.CreateCategoryAsync(client);

        var response = await client.PutAsJsonAsync(
            $"/api/admin/knowledgebase/{SupportTestSeeds.NoSuchId}", KbTestSeeds.ArticleBody(categoryId, [("en", "Title", "Content")]));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.KBARTICLE_NOT_FOUND);
    }

    /// <summary>Moving an article to a category that does not exist answers 404 and leaves the article as it was.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateToAnUnknownCategory_AnswersNotFoundAndChangesNothingAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await KbTestSeeds.CreateCategoryAsync(client);
        var id = await KbTestSeeds.CreateArticleAsync(client, categoryId, "Stays");

        var response = await client.PutAsJsonAsync(
            $"/api/admin/knowledgebase/{id}", KbTestSeeds.ArticleBody(SupportTestSeeds.NoSuchId, [("en", "Changed", "Changed.")]));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.KBCATEGORY_NOT_FOUND);
        var article = await KbTestSeeds.GetArticleAsync(client, id);
        article.GetProperty("categoryId").GetInt32().Should().Be(categoryId);
        article.GetProperty("translations").GetProperty("en").GetProperty("title").GetString().Should().Be("Stays");
    }

    /// <summary>An update without the default language is refused with 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateWithoutTheDefaultLanguage_IsRefusedAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await KbTestSeeds.CreateCategoryAsync(client);
        var id = await KbTestSeeds.CreateArticleAsync(client, categoryId);

        var response = await client.PutAsJsonAsync($"/api/admin/knowledgebase/{id}", KbTestSeeds.ArticleBody(categoryId, [("ru", "Заголовок", "Текст.")]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>An anonymous caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PutAsJsonAsync(
            $"/api/admin/knowledgebase/{SupportTestSeeds.NoSuchId}", KbTestSeeds.ArticleBody(1, [("en", "Title", "Content")]));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
