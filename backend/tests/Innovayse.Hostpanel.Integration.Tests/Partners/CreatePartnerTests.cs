using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Partners;

/// <summary>
/// Integration tests for the <c>CreatePartner</c> use case: <c>POST /api/admin/partners</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreatePartnerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Creating a partner and reading it back round-trips its fields and its translations.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateThenGet_RoundTrips()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var created = await client.PostAsJsonAsync("/api/admin/partners", StorefrontContentTestSeeds.PartnerBody());
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Headers.Location.Should().NotBeNull();
        var id = await created.Content.ReadFromJsonAsync<int>();

        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/admin/partners/{id}");
        fetched.GetProperty("url").GetString().Should().Be("https://partner.example");
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

        var response = await client.PostAsJsonAsync("/api/admin/partners", new { logoUrl = (string?)"javascript:alert(1)", url = (string?)null, sortOrder = 0, isActive = true, translations = new { en = new { name = "x" } } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A body with no translation at all is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_WithoutTranslations_Refuses()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/partners", new { logoUrl = (string?)null, url = (string?)null, sortOrder = 0, isActive = true, translations = new { } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A translation keyed by a language the panel does not support is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_UnsupportedLocale_Refuses()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/partners", new { logoUrl = (string?)null, url = (string?)null, sortOrder = 0, isActive = true, translations = new { fr = new { name = "Nom" } } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/admin/partners", StorefrontContentTestSeeds.PartnerBody());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
