using System.Net;
using Innovayse.Hostpanel.Application.Common;
using static Innovayse.Hostpanel.Integration.Tests.Fixtures.PagedListReads;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <see cref="Innovayse.Hostpanel.Application.Support.Queries.ListKbArticles.ListKbArticlesQuery"/>
/// answers the admin listing (<c>GET /api/admin/knowledgebase</c>): every article, published or not, searched over the title
/// and the body in every language, narrowed by category and published state and paged on the server.
/// The public listing is <c>ListPublishedKbArticlesQuery</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListKbArticlesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can list every knowledge base article, published or not, with all its translations.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanListAllKbArticlesAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await KbTestSeeds.CreateCategoryAsync(client);
        var id = await KbTestSeeds.CreateArticleAsync(client, categoryId, "Listed");

        var page = await PageAsync(client, $"/api/admin/knowledgebase?page=1&pageSize=100&categoryId={categoryId}");

        IdsOf(page).Should().Equal(id);
        var listed = page.GetProperty("items").EnumerateArray().Single();
        listed.GetProperty("translations").GetProperty("en").GetProperty("title").GetString().Should().Be("Listed");
        listed.GetProperty("isPublished").GetBoolean().Should().BeFalse();
    }

    /// <summary>The search matches the title or the body, in any language, in any letter case, and lists an article once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchFindsTitleAndBodyInAnyLanguageAndAnyCase()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await KbTestSeeds.CreateCategoryAsync(admin);
        var token = UniqueToken();
        var inTitle = await KbTestSeeds.CreateArticleAsync(admin, categoryId, $"Title {token}", "Plain.");
        var inBody = await KbTestSeeds.CreateArticleAsync(admin, categoryId, "Plain", $"Body {token}.");
        var inRussian = await KbTestSeeds.CreateArticleAsync(
            admin, KbTestSeeds.ArticleBody(categoryId, [("en", "English only", "Text"), ("ru", $"Заголовок {token}", "Текст")]));
        var inEveryLanguage = await KbTestSeeds.CreateArticleAsync(
            admin, KbTestSeeds.ArticleBody(categoryId, [("en", $"One {token}", token), ("ru", $"Два {token}", token), ("hy", token, token)]));
        await KbTestSeeds.CreateArticleAsync(admin, categoryId, "Unrelated", "Nothing here.");

        var page = await PageAsync(admin, $"/api/admin/knowledgebase?page=1&pageSize=100&categoryId={categoryId}&search={token.ToUpperInvariant()}");

        IdsOf(page).Should().Equal(inTitle, inBody, inRussian, inEveryLanguage);
        page.GetProperty("totalCount").GetInt32().Should().Be(4);
    }

    /// <summary>A percent sign and an underscore typed into the search match themselves, not any text.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchWildcardsAreLiteral()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await KbTestSeeds.CreateCategoryAsync(admin);
        var token = UniqueToken();
        var percent = await KbTestSeeds.CreateArticleAsync(admin, categoryId, $"{token} 50% off");
        await KbTestSeeds.CreateArticleAsync(admin, categoryId, $"{token} 50x off");
        var underscore = await KbTestSeeds.CreateArticleAsync(admin, categoryId, $"{token} a_c");
        await KbTestSeeds.CreateArticleAsync(admin, categoryId, $"{token} abc");
        var route = $"/api/admin/knowledgebase?page=1&pageSize=100&categoryId={categoryId}";

        var percentPage = await PageAsync(admin, $"{route}&search={token}%2050%25");
        var underscorePage = await PageAsync(admin, $"{route}&search={token}%20a_c");
        var bareWildcard = await PageAsync(admin, $"{route}&search={token}%25%25");

        IdsOf(percentPage).Should().Equal(percent);
        IdsOf(underscorePage).Should().Equal(underscore);
        IdsOf(bareWildcard).Should().BeEmpty();
    }

    /// <summary>The category filter keeps the articles of that category only.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CategoryFilterKeepsThatCategorysArticles()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await KbTestSeeds.CreateCategoryAsync(admin);
        var otherId = await KbTestSeeds.CreateCategoryAsync(admin);
        var inCategory = await KbTestSeeds.CreateArticleAsync(admin, categoryId, "Here");
        var elsewhere = await KbTestSeeds.CreateArticleAsync(admin, otherId, "There");

        var page = await PageAsync(admin, $"/api/admin/knowledgebase?page=1&pageSize=100&categoryId={categoryId}");
        var other = await PageAsync(admin, $"/api/admin/knowledgebase?page=1&pageSize=100&categoryId={otherId}");

        IdsOf(page).Should().Equal(inCategory);
        IdsOf(other).Should().Equal(elsewhere);
    }

    /// <summary>The published filter keeps the published articles or the drafts.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsPublishedFilterKeepsPublishedOrDrafts()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await KbTestSeeds.CreateCategoryAsync(admin);
        var published = await KbTestSeeds.CreateArticleAsync(admin, categoryId, "Live", publish: true);
        var draft = await KbTestSeeds.CreateArticleAsync(admin, categoryId, "Draft");
        var route = $"/api/admin/knowledgebase?page=1&pageSize=100&categoryId={categoryId}";

        var publishedPage = await PageAsync(admin, $"{route}&isPublished=true");
        var draftPage = await PageAsync(admin, $"{route}&isPublished=false");
        var both = await PageAsync(admin, route);

        IdsOf(publishedPage).Should().Equal(published);
        IdsOf(draftPage).Should().Equal(draft);
        IdsOf(both).Should().Equal(published, draft);
    }

    /// <summary>The search and every filter narrow together: an article must pass each one.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchAndFiltersNarrowTogether()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await KbTestSeeds.CreateCategoryAsync(admin);
        var otherId = await KbTestSeeds.CreateCategoryAsync(admin);
        var token = UniqueToken();
        var match = await KbTestSeeds.CreateArticleAsync(admin, categoryId, $"Match {token}", publish: true);
        await KbTestSeeds.CreateArticleAsync(admin, categoryId, $"Draft {token}");
        await KbTestSeeds.CreateArticleAsync(admin, otherId, $"Elsewhere {token}", publish: true);
        await KbTestSeeds.CreateArticleAsync(admin, categoryId, "Other text", publish: true);

        var page = await PageAsync(admin, $"/api/admin/knowledgebase?page=1&pageSize=100&search={token}&categoryId={categoryId}&isPublished=true");

        IdsOf(page).Should().Equal(match);
    }

    /// <summary>A filter is applied before paging: the total counts every match and each page holds its slice, oldest first.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FilterIsAppliedBeforePaging()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var categoryId = await KbTestSeeds.CreateCategoryAsync(admin);
        var first = await KbTestSeeds.CreateArticleAsync(admin, categoryId, "One");
        var second = await KbTestSeeds.CreateArticleAsync(admin, categoryId, "Two");
        var third = await KbTestSeeds.CreateArticleAsync(admin, categoryId, "Three");
        var route = $"/api/admin/knowledgebase?pageSize=2&categoryId={categoryId}";

        var pageOne = await PageAsync(admin, $"{route}&page=1");
        var pageTwo = await PageAsync(admin, $"{route}&page=2");
        var beyond = await PageAsync(admin, $"{route}&page=3");

        IdsOf(pageOne).Should().Equal(first, second);
        pageOne.GetProperty("totalCount").GetInt32().Should().Be(3);
        IdsOf(pageTwo).Should().Equal(third);
        IdsOf(beyond).Should().BeEmpty();
    }

    /// <summary>A page below one, no page, or a page size outside the limit is refused with 400 on the page and page size, not corrected.</summary>
    /// <param name="queryString">The paging part of the query string.</param>
    /// <param name="field">The field the refusal must name.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("page=0&pageSize=20", "page")]
    [InlineData("pageSize=20", "page")]
    [InlineData("page=1&pageSize=0", "pagesize")]
    [InlineData("page=1&pageSize=101", "pagesize")]
    public async Task PagingOutsideTheLimitsIsRefused(string queryString, string field)
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/admin/knowledgebase?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RefusedFieldsAsync(response)).Should().Contain(field);
    }

    /// <summary>A search past the limit or a category id that is not positive is refused with 400 on that field.</summary>
    /// <param name="filter">The filter's key and value.</param>
    /// <param name="field">The field the refusal must name.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("search=TOO_LONG", "search")]
    [InlineData("categoryId=0", "categoryid")]
    [InlineData("categoryId=-3", "categoryid")]
    public async Task FilterValueOutOfRangeIsRefused(string filter, string field)
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);
        var value = filter.Replace("TOO_LONG", new string('a', PagingLimits.MaxSearchLength + 1), StringComparison.Ordinal);

        var response = await admin.GetAsync($"/api/admin/knowledgebase?page=1&pageSize=20&{value}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await RefusedFieldsAsync(response)).Should().Contain(field);
    }

    /// <summary>A published state that is not a boolean is refused with 400 by the model binder.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnparsablePublishedStateIsRefused()
    {
        var admin = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/admin/knowledgebase?page=1&pageSize=20&isPublished=maybe");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The admin listing route is closed to a caller with no credential.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminRouteIsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/knowledgebase?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
