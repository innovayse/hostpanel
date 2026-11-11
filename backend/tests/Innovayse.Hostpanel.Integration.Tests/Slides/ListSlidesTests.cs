using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Slides;

/// <summary>
/// Integration tests for the <c>ListSlides</c> use case: <c>GET /api/admin/slides</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListSlidesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A created slide shows up in the admin list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminList_ShowsACreatedSlide()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);
        var created = await client.PostAsJsonAsync("/api/admin/slides", SlidesTestSeeds.CreateBody());
        var id = await created.Content.ReadFromJsonAsync<int>();

        var list = await client.GetFromJsonAsync<JsonElement[]>("/api/admin/slides");

        list!.Select(s => s.GetProperty("id").GetInt32()).Should().Contain(id);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminList_RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/slides");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
