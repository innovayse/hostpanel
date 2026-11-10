using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Slides;
using Innovayse.Hostpanel.Domain.Slides.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Slides;

/// <summary>
/// Integration tests for the <c>ListActiveSlides</c> use case: <c>GET /api/slides</c>, the public
/// storefront feed. Anonymous: the storefront needs it before anyone has signed in.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListActiveSlidesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The fixed moment the seeded rows are stamped with.</summary>
    private static readonly DateTimeOffset SeededAt = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>GET /api/slides is publicly accessible without authentication.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublicList_ReturnsOk_WithoutAuth()
    {
        var response = await factory.CreateClient().GetAsync("/api/slides");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>The public feed excludes an inactive slide and one restricted to a different audience.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublicList_ExcludesInactiveAndWrongAudienceSlides()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);
        var visible = await client.PostAsJsonAsync(
            "/api/admin/slides", SlidesTestSeeds.CreateBody(isActive: true, audience: SlideAudience.All));
        var visibleId = await visible.Content.ReadFromJsonAsync<int>();
        var inactive = await client.PostAsJsonAsync(
            "/api/admin/slides", SlidesTestSeeds.CreateBody(isActive: false, audience: SlideAudience.All));
        var inactiveId = await inactive.Content.ReadFromJsonAsync<int>();
        var authOnly = await client.PostAsJsonAsync(
            "/api/admin/slides", SlidesTestSeeds.CreateBody(isActive: true, audience: SlideAudience.Authenticated));
        var authOnlyId = await authOnly.Content.ReadFromJsonAsync<int>();

        var response = await factory.CreateClient().GetAsync("/api/slides");
        var list = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        list!.Should().Contain(s => s.GetProperty("id").GetInt32() == visibleId);
        list.Should().NotContain(s => s.GetProperty("id").GetInt32() == inactiveId);
        list.Should().NotContain(s => s.GetProperty("id").GetInt32() == authOnlyId);
    }

    /// <summary>The public feed resolves the requested locale over the default when both are stored.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PublicList_ResolvesTheRequestedLocaleOverTheDefault()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);
        var created = await client.PostAsJsonAsync("/api/admin/slides", new
        {
            iconName = "cloud-check",
            brandColor = "#0ea5e9",
            imageUrl = "/uploads/slides/seed.png",
            demoUrl = (string?)null,
            learnMoreUrl = (string?)null,
            productId = (int?)null,
            sortOrder = 0,
            isActive = true,
            targetAudience = SlideAudience.All,
            visibleFrom = (DateTimeOffset?)null,
            visibleUntil = (DateTimeOffset?)null,
            translations = new[]
            {
                new
                {
                    locale = "en",
                    title = "English title",
                    tagline = (string?)null,
                    description = (string?)null,
                    features = (string[]?)null,
                    ctaText = (string?)null,
                    ctaUrl = (string?)null,
                },
                new
                {
                    locale = "hy",
                    title = "Armenian title",
                    tagline = (string?)null,
                    description = (string?)null,
                    features = (string[]?)null,
                    ctaText = (string?)null,
                    ctaUrl = (string?)null,
                },
            },
        });
        var id = await created.Content.ReadFromJsonAsync<int>();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/slides");
        request.Headers.Add("X-Language", "hy");
        var response = await factory.CreateClient().SendAsync(request);
        var list = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        var slide = list!.Single(s => s.GetProperty("id").GetInt32() == id);
        slide.GetProperty("content").GetProperty("title").GetString().Should().Be("Armenian title");
    }

    /// <summary>A query-string audience is ignored: an anonymous caller never receives an Authenticated slide.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListActive_AnonymousAskingForAuthenticated_StillGetsNone()
    {
        var admin = await SlidesTestSeeds.AdminClientAsync(factory);
        var created = await admin.PostAsJsonAsync(
            "/api/admin/slides", SlidesTestSeeds.CreateBody(isActive: true, audience: SlideAudience.Authenticated));
        var id = await created.Content.ReadFromJsonAsync<int>();

        var response = await factory.CreateClient().GetAsync("/api/slides?audience=Authenticated");
        var list = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        list!.Should().NotContain(s => s.GetProperty("id").GetInt32() == id);
    }

    /// <summary>A signed-in caller receives Authenticated slides and not the guest-only ones.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListActive_SignedInCaller_GetsAuthenticatedAndNotGuestSlides()
    {
        var admin = await SlidesTestSeeds.AdminClientAsync(factory);
        var authenticated = await admin.PostAsJsonAsync(
            "/api/admin/slides", SlidesTestSeeds.CreateBody(isActive: true, audience: SlideAudience.Authenticated));
        var authenticatedId = await authenticated.Content.ReadFromJsonAsync<int>();
        var guest = await admin.PostAsJsonAsync(
            "/api/admin/slides", SlidesTestSeeds.CreateBody(isActive: true, audience: SlideAudience.Guest));
        var guestId = await guest.Content.ReadFromJsonAsync<int>();

        var response = await admin.GetAsync("/api/slides");
        var list = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        list!.Should().Contain(s => s.GetProperty("id").GetInt32() == authenticatedId);
        list.Should().NotContain(s => s.GetProperty("id").GetInt32() == guestId);
    }

    /// <summary>A slide with no translation at all is left out of the public feed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListActive_SlideWithoutTranslation_IsSkipped()
    {
        int id;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var slide = Slide.Create(
                new SlideSettings("mdi-server", "#123456", "/img/slide.png", null, null, null, 0, true, SlideAudience.All, null, null),
                SeededAt);
            context.Slides.Add(slide);
            await context.SaveChangesAsync(CancellationToken.None);
            id = slide.Id;
        }

        var response = await factory.CreateClient().GetAsync("/api/slides");
        var list = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        list!.Should().NotContain(s => s.GetProperty("id").GetInt32() == id);
    }

    /// <summary>A slide linked to a product priced only quarterly, semi-annually and biennially carries a from price and no monthly or annual price.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListActive_LinkedProductPricedOnLongCycles_CarriesTheLowestMonthlyEquivalentAsFromPrice()
    {
        var productId = await ProductsTestSeeds.SeedProductWithUsdPricesAsync(
            factory, (BillingCycle.Quarterly, 30m), (BillingCycle.SemiAnnual, 48m), (BillingCycle.Biennial, 120m));
        var admin = await SlidesTestSeeds.AdminClientAsync(factory);
        var created = await admin.PostAsJsonAsync("/api/admin/slides", SlidesTestSeeds.CreateBody(productId: productId));
        var id = await created.Content.ReadFromJsonAsync<int>();

        var response = await factory.CreateClient().GetAsync("/api/slides");
        var list = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        var pricing = list!.Single(x => x.GetProperty("id").GetInt32() == id).GetProperty("pricing");
        pricing.GetProperty("fromPrice").GetDecimal().Should().Be(5m);
        pricing.GetProperty("monthlyPrice").ValueKind.Should().Be(JsonValueKind.Null);
        pricing.GetProperty("annualPrice").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>A slide linked to a product with monthly and annual prices keeps both and gets the cheaper monthly equivalent as its from price.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListActive_LinkedProductPricedMonthlyAndAnnual_KeepsBothAndAddsFromPrice()
    {
        var productId = await ProductsTestSeeds.SeedProductWithUsdPricesAsync(
            factory, (BillingCycle.Monthly, 5m), (BillingCycle.Annual, 48m));
        var admin = await SlidesTestSeeds.AdminClientAsync(factory);
        var created = await admin.PostAsJsonAsync("/api/admin/slides", SlidesTestSeeds.CreateBody(productId: productId));
        var id = await created.Content.ReadFromJsonAsync<int>();

        var response = await factory.CreateClient().GetAsync("/api/slides");
        var list = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        var pricing = list!.Single(x => x.GetProperty("id").GetInt32() == id).GetProperty("pricing");
        pricing.GetProperty("monthlyPrice").GetDecimal().Should().Be(5m);
        pricing.GetProperty("annualPrice").GetDecimal().Should().Be(48m);
        pricing.GetProperty("fromPrice").GetDecimal().Should().Be(4m);
    }

    /// <summary>A slide with no linked product has a null from price.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListActive_SlideWithoutProduct_HasNullFromPrice()
    {
        var admin = await SlidesTestSeeds.AdminClientAsync(factory);
        var created = await admin.PostAsJsonAsync("/api/admin/slides", SlidesTestSeeds.CreateBody());
        var id = await created.Content.ReadFromJsonAsync<int>();

        var response = await factory.CreateClient().GetAsync("/api/slides");
        var list = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        list!.Single(x => x.GetProperty("id").GetInt32() == id)
            .GetProperty("pricing").GetProperty("fromPrice").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>A successful public list is marked cacheable for a short time.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListActive_AnonymousAnswer_CarriesShortPublicCacheHeaders()
    {
        var response = await factory.CreateClient().GetAsync("/api/slides");

        response.Headers.CacheControl!.Public.Should().BeTrue();
        response.Headers.CacheControl.MaxAge.Should().Be(TimeSpan.FromSeconds(60));
        response.Headers.Vary.Should().Contain("X-Language");
    }
}
