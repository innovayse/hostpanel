using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Support.Common;
using Innovayse.Hostpanel.Domain.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// Integration tests for the <c>ListPublishedKbArticles</c> use case: <c>GET /api/knowledgebase/articles</c>. It is
/// public and read-only; it answers in the reader's language and filters by category (name or slug) and search term.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListPublishedKbArticlesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The category filter lists the published articles of that category only, matching its name without regard to case.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CategoryFilter_ListsPublishedArticlesOfThatCategoryOnlyAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var category = $"Cat-{Guid.NewGuid():N}";
        var categoryId = await KbTestSeeds.CreateCategoryAsync(admin, category);
        var otherId = await KbTestSeeds.CreateCategoryAsync(admin);
        var first = await KbTestSeeds.CreateArticleAsync(admin, categoryId, "First", "Body one.", publish: true);
        var second = await KbTestSeeds.CreateArticleAsync(admin, categoryId, "Second", "Body two.", publish: true);
        await KbTestSeeds.CreateArticleAsync(admin, categoryId, "Draft", "Hidden.");
        await KbTestSeeds.CreateArticleAsync(admin, otherId, "Elsewhere", "Other.", publish: true);

        var page = await GetPageAsync($"/api/knowledgebase/articles?category={category.ToUpperInvariant()}");

        page.GetProperty("totalCount").GetInt32().Should().Be(2);
        Ids(page).Should().Equal(first, second);
    }

    /// <summary>The category filter also takes the category's slug, and the rows carry both slugs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CategoryFilter_AcceptsTheSlug_AndRowsCarryTheSlugsAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var marker = Guid.NewGuid().ToString("N")[..10];
        var categoryId = await KbTestSeeds.CreateCategoryAsync(admin, $"Billing Help {marker}");
        var id = await KbTestSeeds.CreateArticleAsync(admin, categoryId, $"Pay an invoice {marker}", "Body.", publish: true);

        var page = await GetPageAsync($"/api/knowledgebase/articles?category=billing-help-{marker}");

        Ids(page).Should().Equal(id);
        var row = page.GetProperty("items").EnumerateArray().Single();
        row.GetProperty("slug").GetString().Should().Be($"pay-an-invoice-{marker}");
        row.GetProperty("categorySlug").GetString().Should().Be($"billing-help-{marker}");
        row.GetProperty("category").GetString().Should().Be($"Billing Help {marker}");
    }

    /// <summary>The search matches the title or the body.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Search_MatchesTitleOrContentAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var category = $"Cat-{Guid.NewGuid():N}";
        var categoryId = await KbTestSeeds.CreateCategoryAsync(admin, category);
        var marker = Guid.NewGuid().ToString("N");
        var inTitle = await KbTestSeeds.CreateArticleAsync(admin, categoryId, $"Title {marker}", "Plain.", publish: true);
        var inContent = await KbTestSeeds.CreateArticleAsync(admin, categoryId, "Plain", $"Body {marker.ToUpperInvariant()}.", publish: true);
        await KbTestSeeds.CreateArticleAsync(admin, categoryId, "Unrelated", "Nothing here.", publish: true);

        var page = await GetPageAsync($"/api/knowledgebase/articles?category={category}&search={marker}");

        Ids(page).Should().Equal(inTitle, inContent);
    }

    /// <summary>The search looks into every language, while the row is shown in the reader's.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Search_FindsATextWrittenInAnotherLanguage_AndShowsTheReadersLanguageAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var category = $"Cat-{Guid.NewGuid():N}";
        var categoryId = await KbTestSeeds.CreateCategoryAsync(admin, category);
        var marker = Guid.NewGuid().ToString("N");
        var id = await KbTestSeeds.CreateArticleAsync(admin, KbTestSeeds.ArticleBody(
            categoryId, [("en", "English title", "English body."), ("ru", $"Русский {marker}", "Русский текст.")], isPublished: true));

        var page = await GetPageAsync($"/api/knowledgebase/articles?category={category}&search={marker}", "en");

        Ids(page).Should().Equal(id);
        page.GetProperty("items").EnumerateArray().Single().GetProperty("title").GetString().Should().Be("English title");
    }

    /// <summary>The row is in the requested language, else in the default one, else in the first language the article has.</summary>
    /// <param name="language">The <c>X-Language</c> sent, or <see langword="null"/> for none.</param>
    /// <param name="expectedTitle">The title expected.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("ru", "Русский заголовок")]
    [InlineData("hy", "English title")]
    [InlineData("de", "English title")]
    [InlineData(null, "English title")]
    public async Task Rows_AreInTheReadersLanguage_ElseTheDefaultAsync(string? language, string expectedTitle)
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var category = $"Cat-{Guid.NewGuid():N}";
        var categoryId = await KbTestSeeds.CreateCategoryAsync(admin, category);
        var id = await KbTestSeeds.CreateArticleAsync(admin, KbTestSeeds.ArticleBody(
            categoryId, [("en", "English title", "English body."), ("ru", "Русский заголовок", "Русский текст.")], isPublished: true));

        var page = await GetPageAsync($"/api/knowledgebase/articles?category={category}", language);

        Ids(page).Should().Equal(id);
        page.GetProperty("items").EnumerateArray().Single().GetProperty("title").GetString().Should().Be(expectedTitle);
    }

    /// <summary>An article with no English text is still listed, in the first language it has.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ArticleWithoutTheDefaultLanguage_IsStillListedAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var category = $"Cat-{Guid.NewGuid():N}";
        var categoryId = await KbTestSeeds.CreateCategoryAsync(admin, category);
        var id = await KbTestSeeds.CreateArticleAsync(admin, categoryId, "English", "Body.", publish: true);
        // A row that came in with the migration is written in its own language only: remove English by hand.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var article = await context.KbArticles.FirstAsync(a => a.Id == id);
            article.ReplaceTranslations(new Dictionary<string, KbArticleTranslationContent> { ["ru"] = new("Только русский", "Текст.") });
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var page = await GetPageAsync($"/api/knowledgebase/articles?category={category}", "en");

        page.GetProperty("items").EnumerateArray().Single().GetProperty("title").GetString().Should().Be("Только русский");
    }

    /// <summary>When one category's name equals another category's slug, the filter means the category that has it as its slug.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CategoryFilter_PrefersASlugOverANameThatLooksLikeOneAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var marker = Guid.NewGuid().ToString("N")[..10];
        var owner = await KbTestSeeds.CreateCategoryAsync(admin, $"Pay Here {marker}");
        var lookalike = await KbTestSeeds.CreateCategoryAsync(admin, $"pay-here-{marker}");
        var inOwner = await KbTestSeeds.CreateArticleAsync(admin, owner, "Owner article", "Body.", publish: true);
        var inLookalike = await KbTestSeeds.CreateArticleAsync(admin, lookalike, "Lookalike article", "Body.", publish: true);

        var bySlug = await GetPageAsync($"/api/knowledgebase/articles?category=pay-here-{marker}");
        var byName = await GetPageAsync($"/api/knowledgebase/articles?category=Pay%20Here%20{marker}");
        var byOtherSlug = await GetPageAsync($"/api/knowledgebase/articles?category=pay-here-{marker}-2");

        Ids(bySlug).Should().Equal(inOwner);
        Ids(byName).Should().Equal(inOwner);
        Ids(byOtherSlug).Should().Equal(inLookalike);
    }

    /// <summary>Paging returns the requested slice.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Paging_ReturnsTheRequestedSliceAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var category = $"Cat-{Guid.NewGuid():N}";
        var categoryId = await KbTestSeeds.CreateCategoryAsync(admin, category);
        await KbTestSeeds.CreateArticleAsync(admin, categoryId, "A", "a", publish: true);
        await KbTestSeeds.CreateArticleAsync(admin, categoryId, "B", "b", publish: true);
        var third = await KbTestSeeds.CreateArticleAsync(admin, categoryId, "C", "c", publish: true);

        var page = await GetPageAsync($"/api/knowledgebase/articles?category={category}&page=2&pageSize=2");

        Ids(page).Should().Equal(third);
        page.GetProperty("pageNumber").GetInt32().Should().Be(2);
        page.GetProperty("totalPages").GetInt32().Should().Be(2);
    }

    /// <summary>Rows carry a plain text excerpt and never the body.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Rows_CarryAPlainTextExcerptAndNoBodyAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var category = $"Cat-{Guid.NewGuid():N}";
        var categoryId = await KbTestSeeds.CreateCategoryAsync(admin, category);
        var longText = new string('x', 300);
        await KbTestSeeds.CreateArticleAsync(admin, categoryId, "Guide", $"<p>Step <strong>one</strong></p>\n<p>{longText}</p>", publish: true);

        var page = await GetPageAsync($"/api/knowledgebase/articles?category={category}");

        var row = page.GetProperty("items").EnumerateArray().Single();
        row.TryGetProperty("content", out _).Should().BeFalse();
        row.GetProperty("title").GetString().Should().Be("Guide");
        row.GetProperty("category").GetString().Should().Be(category);
        var excerpt = row.GetProperty("excerpt").GetString();
        excerpt.Should().StartWith("Step one x").And.NotContain("<").And.HaveLength(KbArticleExcerpt.MaxLength);
    }

    /// <summary>A typed % or _ matches itself instead of widening the search.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchWithWildcardCharacters_MatchesThemLiterallyAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var category = $"Cat-{Guid.NewGuid():N}";
        var categoryId = await KbTestSeeds.CreateCategoryAsync(admin, category);
        var plain = await KbTestSeeds.CreateArticleAsync(admin, categoryId, "Plain", "Nothing special here.", publish: true);
        var literal = await KbTestSeeds.CreateArticleAsync(admin, categoryId, "Literal", "Discount of 100% today.", publish: true);

        var wildcard = await GetPageAsync($"/api/knowledgebase/articles?category={category}&search=%25");
        var underscore = await GetPageAsync($"/api/knowledgebase/articles?category={category}&search=_");

        Ids(wildcard).Should().Equal(literal);
        underscore.GetProperty("totalCount").GetInt32().Should().Be(0);
        plain.Should().NotBe(literal);
    }

    /// <summary>The excerpt decodes entities and ends at a word boundary.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Excerpt_DecodesEntitiesAndEndsAtAWordBoundaryAsync()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var category = $"Cat-{Guid.NewGuid():N}";
        var categoryId = await KbTestSeeds.CreateCategoryAsync(admin, category);
        var words = string.Join(' ', Enumerable.Repeat("word", 60));
        await KbTestSeeds.CreateArticleAsync(admin, categoryId, "Words", $"<p>Fish &amp; chips {words}</p>", publish: true);

        var page = await GetPageAsync($"/api/knowledgebase/articles?category={category}");

        var excerpt = page.GetProperty("items").EnumerateArray().Single().GetProperty("excerpt").GetString();
        excerpt.Should().StartWith("Fish & chips word").And.EndWith("word").And.NotContain("&amp;");
        excerpt!.Length.Should().BeLessThanOrEqualTo(KbArticleExcerpt.MaxLength);
    }

    /// <summary>An overlong search text or category filter is refused with 400.</summary>
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

    /// <summary>A page size outside the allowed range is refused with 400.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PageSizeOutOfRange_IsRefusedAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/knowledgebase/articles?pageSize=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Gets one page of the public list.</summary>
    /// <param name="url">The route with its query string.</param>
    /// <param name="language">The <c>X-Language</c> to send, or <see langword="null"/> for none.</param>
    /// <returns>The page as JSON.</returns>
    private async Task<JsonElement> GetPageAsync(string url, string? language = null)
    {
        var response = await StorefrontContentTestSeeds.GetPublicAsync(factory, url, language);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>The article ids of a page, in order.</summary>
    /// <param name="page">The page as JSON.</param>
    /// <returns>The ids.</returns>
    private static int[] Ids(JsonElement page) =>
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).ToArray();
}
