using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Testimonials;

/// <summary>
/// Integration tests for the <c>CreateTestimonial</c> use case: <c>POST /api/admin/testimonials</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateTestimonialTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Creating a testimonial and reading it back round-trips its fields and its translations.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateThenGet_RoundTrips()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var created = await client.PostAsJsonAsync("/api/admin/testimonials", StorefrontContentTestSeeds.TestimonialBody());
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Headers.Location.Should().NotBeNull();
        var id = await created.Content.ReadFromJsonAsync<int>();

        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/admin/testimonials/{id}");
        fetched.GetProperty("company").GetString().Should().Be("Acme");
        fetched.GetProperty("isActive").GetBoolean().Should().BeTrue();
        var translations = fetched.GetProperty("translations");
        translations.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("en", "hy");
        translations.GetProperty("en").GetProperty("quote").GetString().Should().Be("English quote");
        translations.GetProperty("hy").GetProperty("quote").GetString().Should().Be("Armenian quote");
    }

    /// <summary>The admin-set date round-trips through the admin read.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateThenGet_RoundTripsPublishedAt()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var date = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero);

        var id = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/testimonials", StorefrontContentTestSeeds.TestimonialBody(publishedAt: date));

        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/admin/testimonials/{id}");
        fetched.GetProperty("publishedAt").GetDateTimeOffset().Should().Be(date);
    }

    /// <summary>A date in the future is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_FuturePublishedAt_Refuses()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var future = new DateTimeOffset(2999, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var response = await client.PostAsJsonAsync("/api/admin/testimonials", StorefrontContentTestSeeds.TestimonialBody(publishedAt: future));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A field the validator refuses answers 400 with the validation code, and nothing is stored.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_InvalidField_RefusesWithValidationFailure()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/testimonials", new { name = "", company = (string?)null, avatarUrl = (string?)null, rating = 9, sortOrder = 0, isActive = true, translations = new { en = new { role = (string?)null, quote = "x" } } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A body with no translation at all is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_WithoutTranslations_Refuses()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/testimonials", new { name = "A", company = (string?)null, avatarUrl = (string?)null, rating = 5, sortOrder = 0, isActive = true, translations = new { } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A translation keyed by a language the panel does not support is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_UnsupportedLocale_Refuses()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/testimonials", new { name = "A", company = (string?)null, avatarUrl = (string?)null, rating = 5, sortOrder = 0, isActive = true, translations = new { fr = new { role = (string?)null, quote = "Bonjour" } } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/admin/testimonials", StorefrontContentTestSeeds.TestimonialBody());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
