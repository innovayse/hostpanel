using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Partners;

/// <summary>
/// Integration tests for the <c>ListActivePartners</c> use case: <c>GET /api/public/partners</c>, the public
/// storefront feed. Anonymous: the storefront needs it before anyone has signed in.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListActivePartnersTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The feed is publicly accessible without authentication.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublicList_ReturnsOk_WithoutAuth()
    {
        var response = await StorefrontContentTestSeeds.GetPublicAsync(factory, "/api/public/partners");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>The feed carries the contract's flat shape and leaves out admin-only members.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublicList_ReturnsTheFlatContractShape()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var id = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/partners", StorefrontContentTestSeeds.PartnerBody(sortOrder: 3));

        var response = await StorefrontContentTestSeeds.GetPublicAsync(factory, "/api/public/partners", "en");
        var list = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        var item = list!.Single(x => x.GetProperty("id").GetInt32() == id);
        item.GetProperty("sortOrder").GetInt32().Should().Be(3);
        item.GetProperty("name").GetString().Should().Be("English name");
        item.TryGetProperty("translations", out _).Should().BeFalse();
        item.GetProperty("logoUrl").GetString().Should().Be("/uploads/branding/content/seed/logo.png");
        item.GetProperty("url").GetString().Should().Be("https://partner.example");
        item.TryGetProperty("isActive", out _).Should().BeFalse();
    }

    /// <summary>The feed excludes an inactive record and orders the rest by sort order.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublicList_ExcludesInactive_AndOrdersBySortOrder()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var second = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/partners", StorefrontContentTestSeeds.PartnerBody(sortOrder: 60));
        var first = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/partners", StorefrontContentTestSeeds.PartnerBody(sortOrder: 50));
        var inactive = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/partners", StorefrontContentTestSeeds.PartnerBody(sortOrder: 55, isActive: false));

        var response = await StorefrontContentTestSeeds.GetPublicAsync(factory, "/api/public/partners");
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
        var id = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/partners", StorefrontContentTestSeeds.PartnerBody());

        var response = await StorefrontContentTestSeeds.GetPublicAsync(factory, "/api/public/partners", "hy");
        var list = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        list!.Single(x => x.GetProperty("id").GetInt32() == id)
            .GetProperty("name").GetString().Should().Be("Armenian name");
    }

    /// <summary>A locale with no stored translation falls back to the default locale.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublicList_FallsBackToTheDefaultLocale()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var id = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/partners", StorefrontContentTestSeeds.PartnerBody());

        var response = await StorefrontContentTestSeeds.GetPublicAsync(factory, "/api/public/partners", "ru");
        var list = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        list!.Single(x => x.GetProperty("id").GetInt32() == id)
            .GetProperty("name").GetString().Should().Be("English name");
    }
}
