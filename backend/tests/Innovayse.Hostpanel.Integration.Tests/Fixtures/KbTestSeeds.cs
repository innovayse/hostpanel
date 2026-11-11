using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>Creates knowledge base categories and articles through the admin API, for the tests that read them back.</summary>
public static class KbTestSeeds
{
    /// <summary>Creates a knowledge base category as admin and returns its id.</summary>
    /// <param name="admin">A client authenticated as Admin.</param>
    /// <param name="name">The category name, or <see langword="null"/> for a unique one.</param>
    /// <returns>The new category's id.</returns>
    public static async Task<int> CreateCategoryAsync(HttpClient admin, string? name = null)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/knowledgebase/categories", new
        {
            name = name ?? $"Category-{Guid.NewGuid():N}",
            description = "A category.",
            isHidden = false,
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return int.Parse((await response.Content.ReadAsStringAsync()).Trim());
    }

    /// <summary>Reads a category as the admin list shows it.</summary>
    /// <param name="admin">A client authenticated as Admin.</param>
    /// <param name="id">The category id.</param>
    /// <returns>The category row.</returns>
    public static async Task<JsonElement> GetCategoryAsync(HttpClient admin, int id)
    {
        var categories = await admin.GetFromJsonAsync<JsonElement[]>("/api/admin/knowledgebase/categories");
        return categories!.Single(c => c.GetProperty("id").GetInt32() == id);
    }

    /// <summary>The request body that creates or updates an article.</summary>
    /// <param name="categoryId">The article's category.</param>
    /// <param name="translations">The text of each locale as (locale, title, content).</param>
    /// <param name="isPublished">Whether the article is created published (create only).</param>
    /// <returns>An object to post as JSON.</returns>
    public static object ArticleBody(int categoryId, (string Locale, string Title, string Content)[] translations, bool isPublished = false) =>
        new
        {
            categoryId,
            translations = translations.ToDictionary(t => t.Locale, t => new { title = t.Title, content = t.Content }),
            isPublished,
        };

    /// <summary>Creates an article with an English translation as admin and returns its id.</summary>
    /// <param name="admin">A client authenticated as Admin.</param>
    /// <param name="categoryId">The article's category.</param>
    /// <param name="title">The English title, or <see langword="null"/> for a unique one.</param>
    /// <param name="content">The English body.</param>
    /// <param name="publish">Whether to publish the article after creating it.</param>
    /// <returns>The new article's id.</returns>
    public static async Task<int> CreateArticleAsync(
        HttpClient admin, int categoryId, string? title = null, string content = "Body text.", bool publish = false)
    {
        var id = await CreateArticleAsync(admin, ArticleBody(categoryId, [("en", title ?? $"Article-{Guid.NewGuid():N}", content)]));
        if (publish)
        {
            (await admin.PutAsync($"/api/admin/knowledgebase/{id}/publish", content: null))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        return id;
    }

    /// <summary>Creates an article from a request body as admin and returns its id.</summary>
    /// <param name="admin">A client authenticated as Admin.</param>
    /// <param name="body">The request body, see <see cref="ArticleBody"/>.</param>
    /// <returns>The new article's id.</returns>
    public static async Task<int> CreateArticleAsync(HttpClient admin, object body)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/knowledgebase", body);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return int.Parse((await response.Content.ReadAsStringAsync()).Trim());
    }

    /// <summary>Reads an article as the admin sees it.</summary>
    /// <param name="admin">A client authenticated as Admin.</param>
    /// <param name="id">The article id.</param>
    /// <returns>The admin view of the article.</returns>
    public static async Task<JsonElement> GetArticleAsync(HttpClient admin, int id)
    {
        var response = await admin.GetAsync($"/api/admin/knowledgebase/{id}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
