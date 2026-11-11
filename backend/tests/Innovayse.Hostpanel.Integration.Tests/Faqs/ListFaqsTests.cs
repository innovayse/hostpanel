using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Faqs;

/// <summary>
/// Integration tests for the <c>ListFaqs</c> use case: <c>GET /api/admin/faqs</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListFaqsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The admin list holds active and inactive records, ordered by sort order.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ReturnsEveryRecord_OrderedBySortOrder()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var later = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/faqs", StorefrontContentTestSeeds.FaqBody(sortOrder: 20));
        var hidden = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/faqs", StorefrontContentTestSeeds.FaqBody(sortOrder: 10, isActive: false));

        var list = await client.GetFromJsonAsync<JsonElement[]>("/api/admin/faqs");

        var ids = list!.Select(x => x.GetProperty("id").GetInt32()).ToList();
        ids.Should().Contain(new[] { later, hidden });
        ids.IndexOf(hidden).Should().BeLessThan(ids.IndexOf(later));
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/faqs");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
