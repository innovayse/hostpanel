using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Locations;

/// <summary>
/// Integration tests for the <c>CreateLocation</c> use case: <c>POST /api/admin/locations</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateLocationTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Creating a location and reading it back round-trips its fields and its translations.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateThenGet_RoundTrips()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var created = await client.PostAsJsonAsync("/api/admin/locations", StorefrontContentTestSeeds.LocationBody());
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Headers.Location.Should().NotBeNull();
        var id = await created.Content.ReadFromJsonAsync<int>();

        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/admin/locations/{id}");
        fetched.GetProperty("countryCode").GetString().Should().Be("DE");
        fetched.GetProperty("isActive").GetBoolean().Should().BeTrue();
        var translations = fetched.GetProperty("translations");
        translations.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("en", "hy");
        translations.GetProperty("en").GetProperty("name").GetString().Should().Be("English name");
        translations.GetProperty("hy").GetProperty("name").GetString().Should().Be("Armenian name");
    }

    /// <summary>A field the validator refuses answers 400 with the validation code, and nothing is stored.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_InvalidField_RefusesWithValidationFailure()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/locations", new { countryCode = "D1", sortOrder = 0, isActive = true, translations = new { en = new { name = "x", city = (string?)null, latencyNote = (string?)null } } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A body with no translation at all is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_WithoutTranslations_Refuses()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/locations", new { countryCode = "de", sortOrder = 0, isActive = true, translations = new { } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A translation keyed by a language the panel does not support is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_UnsupportedLocale_Refuses()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/locations", new { countryCode = "de", sortOrder = 0, isActive = true, translations = new { fr = new { name = "Nom", city = (string?)null, latencyNote = (string?)null } } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/admin/locations", StorefrontContentTestSeeds.LocationBody());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
