using System.Net;
using System.Net.Http.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <see cref="Innovayse.Hostpanel.Application.Support.Queries.ListKbArticles.ListKbArticlesQuery"/>
/// answers the admin listing (<c>GET /api/admin/knowledgebase</c>, every article, published or not).
/// The public listing is <c>ListPublishedKbArticlesQuery</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListKbArticlesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can list every knowledge base article, published or not.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanListAllKbArticlesAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var id = await KbTestSeeds.CreateArticleAsync(client, await KbTestSeeds.CreateCategoryAsync(client), "Listed");

        var response = await client.GetAsync("/api/admin/knowledgebase");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var articles = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement[]>();
        var listed = articles!.Single(a => a.GetProperty("id").GetInt32() == id);
        listed.GetProperty("translations").GetProperty("en").GetProperty("title").GetString().Should().Be("Listed");
    }

    /// <summary>The admin listing route is closed to a caller with no credential.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminRouteIsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/knowledgebase");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
