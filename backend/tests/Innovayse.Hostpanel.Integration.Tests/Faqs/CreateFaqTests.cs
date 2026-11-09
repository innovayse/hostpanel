using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Faqs.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Faqs;

/// <summary>
/// Integration tests for the <c>CreateFaq</c> use case: <c>POST /api/admin/faqs</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateFaqTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Creating a question and reading it back round-trips its fields and its translations.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateThenGet_RoundTrips()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var created = await client.PostAsJsonAsync("/api/admin/faqs", StorefrontContentTestSeeds.FaqBody());
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Headers.Location.Should().NotBeNull();
        var id = await created.Content.ReadFromJsonAsync<int>();

        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/admin/faqs/{id}");
        fetched.GetProperty("category").GetString().Should().Be(nameof(FaqCategory.General));
        fetched.GetProperty("isActive").GetBoolean().Should().BeTrue();
        var translations = fetched.GetProperty("translations");
        translations.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("en", "hy");
        translations.GetProperty("en").GetProperty("question").GetString().Should().Be("English question?");
        translations.GetProperty("hy").GetProperty("question").GetString().Should().Be("Armenian question?");
    }

    /// <summary>A field the validator refuses answers 400 with the validation code, and nothing is stored.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_InvalidField_RefusesWithValidationFailure()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/faqs", new { category = "Nonsense", sortOrder = 0, isActive = true, translations = new { en = new { question = "x?", answer = "y" } } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A body with no translation at all is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_WithoutTranslations_Refuses()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/faqs", new { category = "general", sortOrder = 0, isActive = true, translations = new { } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A translation keyed by a language the panel does not support is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_UnsupportedLocale_Refuses()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/faqs", new { category = "general", sortOrder = 0, isActive = true, translations = new { fr = new { question = "Comment?", answer = "Ainsi." } } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A question without the default-locale translation is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_WithoutDefaultLocale_Refuses()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/faqs", new { category = "general", sortOrder = 0, isActive = true, translations = new { ru = new { question = "Как?", answer = "Так." } } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/admin/faqs", StorefrontContentTestSeeds.FaqBody());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
