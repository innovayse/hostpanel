using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Support.Common;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>GET /api/knowledgebase/articles</c>: the anonymous, paged listing of published articles,
/// narrowed by category and search term in the query and answered as one page envelope.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListPublishedKbArticlesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Only published articles of the requested category are listed, drafts left out, with the unpaged total.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CategoryFilter_ListsPublishedArticlesOfThatCategoryOnlyAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var category = $"Cat-{Guid.NewGuid():N}";
        var first = await CreateArticleAsync(admin, "First", "Body one.", category, publish: true);
        var second = await CreateArticleAsync(admin, "Second", "Body two.", category, publish: true);
        await CreateArticleAsync(admin, "Draft", "Hidden.", category, publish: false);
        await CreateArticleAsync(admin, "Elsewhere", "Other.", $"Other-{Guid.NewGuid():N}", publish: true);

        var page = await GetPageAsync($"/api/knowledgebase/articles?category={category.ToUpperInvariant()}");

        page.GetProperty("totalCount").GetInt32().Should().Be(2);
        Ids(page).Should().Equal(first, second);
    }

    /// <summary>The search term matches title or content case-insensitively.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Search_MatchesTitleOrContentAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var category = $"Cat-{Guid.NewGuid():N}";
        var marker = Guid.NewGuid().ToString("N");
        var inTitle = await CreateArticleAsync(admin, $"Title {marker}", "Plain.", category, publish: true);
        var inContent = await CreateArticleAsync(admin, "Plain", $"Body {marker.ToUpperInvariant()}.", category, publish: true);
        await CreateArticleAsync(admin, "Unrelated", "Nothing here.", category, publish: true);

        var page = await GetPageAsync($"/api/knowledgebase/articles?category={category}&search={marker}");

        Ids(page).Should().Equal(inTitle, inContent);
    }

    /// <summary>Page and page size slice the ordered result and the envelope reports the paging.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Paging_ReturnsTheRequestedSliceAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var category = $"Cat-{Guid.NewGuid():N}";
        await CreateArticleAsync(admin, "A", "a", category, publish: true);
        await CreateArticleAsync(admin, "B", "b", category, publish: true);
        var third = await CreateArticleAsync(admin, "C", "c", category, publish: true);

        var page = await GetPageAsync($"/api/knowledgebase/articles?category={category}&page=2&pageSize=2");

        Ids(page).Should().Equal(third);
        page.GetProperty("pageNumber").GetInt32().Should().Be(2);
        page.GetProperty("totalPages").GetInt32().Should().Be(2);
    }

    /// <summary>Each row carries a plain-text excerpt of the body and never the body itself.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Rows_CarryAPlainTextExcerptAndNoBodyAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var category = $"Cat-{Guid.NewGuid():N}";
        var longText = new string('x', 300);
        await CreateArticleAsync(admin, "Guide", $"<p>Step <strong>one</strong></p>\n<p>{longText}</p>", category, publish: true);

        var page = await GetPageAsync($"/api/knowledgebase/articles?category={category}");

        var row = page.GetProperty("items").EnumerateArray().Single();
        row.TryGetProperty("content", out _).Should().BeFalse();
        row.GetProperty("title").GetString().Should().Be("Guide");
        row.GetProperty("category").GetString().Should().Be(category);
        var excerpt = row.GetProperty("excerpt").GetString();
        excerpt.Should().StartWith("Step one x").And.NotContain("<").And.HaveLength(KbArticleExcerpt.MaxLength);
    }

    /// <summary>A search term made of wildcard characters matches itself, not every article.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchWithWildcardCharacters_MatchesThemLiterallyAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var category = $"Cat-{Guid.NewGuid():N}";
        var plain = await CreateArticleAsync(admin, "Plain", "Nothing special here.", category, publish: true);
        var literal = await CreateArticleAsync(admin, "Literal", "Discount of 100% today.", category, publish: true);

        var wildcard = await GetPageAsync($"/api/knowledgebase/articles?category={category}&search=%25");
        var underscore = await GetPageAsync($"/api/knowledgebase/articles?category={category}&search=_");

        Ids(wildcard).Should().Equal(literal);
        underscore.GetProperty("totalCount").GetInt32().Should().Be(0);
        plain.Should().NotBe(literal);
    }

    /// <summary>An excerpt decodes HTML entities and ends at a word boundary.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Excerpt_DecodesEntitiesAndEndsAtAWordBoundaryAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var category = $"Cat-{Guid.NewGuid():N}";
        var words = string.Join(' ', Enumerable.Repeat("word", 60));
        await CreateArticleAsync(admin, "Words", $"<p>Fish &amp; chips {words}</p>", category, publish: true);

        var page = await GetPageAsync($"/api/knowledgebase/articles?category={category}");

        var excerpt = page.GetProperty("items").EnumerateArray().Single().GetProperty("excerpt").GetString();
        excerpt.Should().StartWith("Fish & chips word").And.EndWith("word").And.NotContain("&amp;");
        excerpt!.Length.Should().BeLessThanOrEqualTo(KbArticleExcerpt.MaxLength);
    }

    /// <summary>A search text or category longer than the bound is refused as a validation error.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OverlongSearchOrCategory_IsRefusedAsync()
    {
        var client = factory.CreateClient();

        var search = await client.GetAsync($"/api/knowledgebase/articles?search={new string('a', 201)}");
        var category = await client.GetAsync($"/api/knowledgebase/articles?category={new string('a', 256)}");

        search.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        category.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A page size outside the allowed range is refused as a validation error.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PageSizeOutOfRange_IsRefusedAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/knowledgebase/articles?pageSize=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Creates an article through the admin API and publishes it when asked.</summary>
    /// <param name="admin">An authenticated admin client.</param>
    /// <param name="title">Article title.</param>
    /// <param name="content">Article body.</param>
    /// <param name="category">Category name.</param>
    /// <param name="publish">Whether to publish the article after creating it.</param>
    /// <returns>The new article id.</returns>
    private static async Task<int> CreateArticleAsync(HttpClient admin, string title, string content, string category, bool publish)
    {
        var created = await admin.PostAsJsonAsync("/api/admin/knowledgebase", new { title, content, category });
        created.EnsureSuccessStatusCode();
        var id = int.Parse((await created.Content.ReadAsStringAsync()).Trim());
        if (publish)
        {
            (await admin.PutAsync($"/api/admin/knowledgebase/{id}/publish", content: null))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        return id;
    }

    /// <summary>Reads the public listing anonymously and returns its page envelope.</summary>
    /// <param name="url">The listing URL with its query string.</param>
    /// <returns>The root JSON element of the page envelope.</returns>
    private async Task<JsonElement> GetPageAsync(string url)
    {
        var response = await factory.CreateClient().GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Extracts the article ids on a page, in order.</summary>
    /// <param name="page">The page envelope.</param>
    /// <returns>The ids of the listed articles.</returns>
    private static int[] Ids(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).ToArray();
}
