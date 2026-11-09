using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Projects;

/// <summary>
/// Integration tests for the <c>ListActiveProjects</c> use case: <c>GET /api/public/projects</c>, the public
/// storefront feed. Anonymous: the storefront needs it before anyone has signed in.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListActiveProjectsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The feed is publicly accessible without authentication.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublicList_ReturnsOk_WithoutAuth()
    {
        var response = await StorefrontContentTestSeeds.GetPublicAsync(factory, "/api/public/projects");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>The feed carries the contract's flat shape and leaves out admin-only members.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublicList_ReturnsTheFlatContractShape()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var id = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/projects", StorefrontContentTestSeeds.ProjectBody(sortOrder: 3));

        var response = await StorefrontContentTestSeeds.GetPublicAsync(factory, "/api/public/projects", "en");
        var list = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        var item = list!.Single(x => x.GetProperty("id").GetInt32() == id);
        item.GetProperty("sortOrder").GetInt32().Should().Be(3);
        item.GetProperty("title").GetString().Should().Be("English title");
        item.TryGetProperty("translations", out _).Should().BeFalse();
        item.GetProperty("slug").GetString().Should().StartWith("project-");
        item.GetProperty("imageUrls").EnumerateArray().Should().ContainSingle();
        item.TryGetProperty("isActive", out _).Should().BeFalse();
    }

    /// <summary>The feed excludes an inactive record and orders the rest by sort order.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublicList_ExcludesInactive_AndOrdersBySortOrder()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var second = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/projects", StorefrontContentTestSeeds.ProjectBody(sortOrder: 60));
        var first = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/projects", StorefrontContentTestSeeds.ProjectBody(sortOrder: 50));
        var inactive = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/projects", StorefrontContentTestSeeds.ProjectBody(sortOrder: 55, isActive: false));

        var response = await StorefrontContentTestSeeds.GetPublicAsync(factory, "/api/public/projects");
        var list = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        var ids = list!.Select(x => x.GetProperty("id").GetInt32()).ToList();
        ids.Should().NotContain(inactive);
        ids.IndexOf(first).Should().BeLessThan(ids.IndexOf(second));
    }

    /// <summary>The feed resolves the requested locale over the default when both are stored.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublicList_ResolvesTheRequestedLocale()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var id = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/projects", StorefrontContentTestSeeds.ProjectBody());

        var response = await StorefrontContentTestSeeds.GetPublicAsync(factory, "/api/public/projects", "hy");
        var list = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        list!.Single(x => x.GetProperty("id").GetInt32() == id)
            .GetProperty("title").GetString().Should().Be("Armenian title");
    }

    /// <summary>A locale with no stored translation falls back to the default locale.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublicList_FallsBackToTheDefaultLocale()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var id = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/projects", StorefrontContentTestSeeds.ProjectBody());

        var response = await StorefrontContentTestSeeds.GetPublicAsync(factory, "/api/public/projects", "ru");
        var list = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        list!.Single(x => x.GetProperty("id").GetInt32() == id)
            .GetProperty("title").GetString().Should().Be("English title");
    }
}
