using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Slides;

/// <summary>
/// Integration tests for the <c>CreateSlide</c> use case: <c>POST /api/admin/slides</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateSlideTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Creating a slide and reading it back round-trips its fields and translation.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateThenGet_RoundTrips()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);

        var created = await client.PostAsJsonAsync("/api/admin/slides", SlidesTestSeeds.CreateBody());
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Headers.Location.Should().NotBeNull();
        var id = await created.Content.ReadFromJsonAsync<int>();

        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/admin/slides/{id}");
        fetched.GetProperty("iconName").GetString().Should().Be("cloud-check");
        fetched.GetProperty("isActive").GetBoolean().Should().BeTrue();
        var translations = fetched.GetProperty("translations").EnumerateArray().ToList();
        translations.Should().ContainSingle();
        translations[0].GetProperty("locale").GetString().Should().Be("en");
        translations[0].GetProperty("title").GetString().Should().Be("Cloud hosting");
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/admin/slides", SlidesTestSeeds.CreateBody());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A negative sort order is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_NegativeSortOrder_RefusesWithValidationError()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/slides", SlidesTestSeeds.CreateBody(sortOrder: -1));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await SlidesTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>Builds a create body from the seed body with the named fields replaced.</summary>
    /// <param name="imageUrl">The slide image link.</param>
    /// <param name="demoUrl">The demo link.</param>
    /// <param name="translations">The translations to send.</param>
    /// <returns>The request body.</returns>
    private static object BodyWith(string imageUrl, string? demoUrl, object[] translations) => new
    {
        iconName = "cloud-check",
        brandColor = "#0ea5e9",
        imageUrl,
        demoUrl,
        learnMoreUrl = (string?)null,
        productId = (int?)null,
        sortOrder = 0,
        isActive = true,
        targetAudience = 0,
        visibleFrom = (DateTimeOffset?)null,
        visibleUntil = (DateTimeOffset?)null,
        translations,
    };

    /// <summary>Builds one translation of the create body.</summary>
    /// <param name="locale">Locale code.</param>
    /// <param name="tagline">Subtitle.</param>
    /// <returns>The translation.</returns>
    private static object Translation(string locale, string? tagline = null) => new
    {
        locale,
        title = "Title",
        tagline,
        description = (string?)null,
        features = (string[]?)null,
        ctaText = (string?)null,
        ctaUrl = (string?)null,
    };

    /// <summary>A link that is not a web address is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_ScriptLink_Refuses()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/admin/slides", BodyWith("/img/a.png", "javascript:alert(1)", [Translation("en")]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>An image link that is not a web address is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_DataImageLink_Refuses()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/admin/slides", BodyWith("data:text/html;base64,AAAA", null, [Translation("en")]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The same locale twice is refused rather than reaching the unique index.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_DuplicateLocale_Refuses()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/admin/slides", BodyWith("/img/a.png", null, [Translation("en"), Translation("en")]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A locale the panel does not support is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_UnsupportedLocale_Refuses()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/admin/slides", BodyWith("/img/a.png", null, [Translation("en"), Translation("fr")]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A slide without the default-locale translation is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_WithoutDefaultLocale_Refuses()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/admin/slides", BodyWith("/img/a.png", null, [Translation("ru")]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A text longer than its column is a 400, not a database failure.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_OverlongTagline_Refuses()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/admin/slides", BodyWith("/img/a.png", null, [Translation("en", new string('x', 501))]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A slide that promotes a product that does not exist answers 404 with <c>PRODUCT_NOT_FOUND</c> and writes nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_UnknownProduct_RefusesNotFound()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/admin/slides", SlidesTestSeeds.CreateBody(productId: SlidesTestSeeds.NoSuchId));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SlidesTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PRODUCT_NOT_FOUND);
    }

    /// <summary>A slide that promotes an existing product is created and keeps the product link.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_ExistingProduct_KeepsTheProductLink()
    {
        var client = await SlidesTestSeeds.AdminClientAsync(factory);
        int productId;
        using (var scope = factory.Services.CreateScope())
        {
            productId = await DomainsTestSeeds.SeedHostingProductAsync(scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>());
        }

        var created = await client.PostAsJsonAsync("/api/admin/slides", SlidesTestSeeds.CreateBody(productId: productId));

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = await created.Content.ReadFromJsonAsync<int>();
        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/admin/slides/{id}");
        fetched.GetProperty("productId").GetInt32().Should().Be(productId);
    }
}
