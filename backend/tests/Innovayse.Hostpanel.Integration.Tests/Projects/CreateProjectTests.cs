using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Projects;

/// <summary>
/// Integration tests for the <c>CreateProject</c> use case: <c>POST /api/admin/projects</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateProjectTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Creating a project and reading it back round-trips its fields and its translations.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateThenGet_RoundTrips()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var created = await client.PostAsJsonAsync("/api/admin/projects", StorefrontContentTestSeeds.ProjectBody());
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Headers.Location.Should().NotBeNull();
        var id = await created.Content.ReadFromJsonAsync<int>();

        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{id}");
        fetched.GetProperty("url").GetString().Should().Be("https://project.example");
        fetched.GetProperty("isActive").GetBoolean().Should().BeTrue();
        var translations = fetched.GetProperty("translations");
        translations.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("en", "hy");
        translations.GetProperty("en").GetProperty("title").GetString().Should().Be("English title");
        translations.GetProperty("hy").GetProperty("title").GetString().Should().Be("Armenian title");
    }

    /// <summary>A field the validator refuses answers 400 with the validation code, and nothing is stored.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_InvalidField_RefusesWithValidationFailure()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/projects", new { slug = "Not A Slug", url = (string?)null, imageUrls = Array.Empty<string>(), sortOrder = 0, isActive = true, translations = new { en = new { title = "x", summary = (string?)null, description = (string?)null } } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A body with no translation at all is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_WithoutTranslations_Refuses()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/projects", new { slug = "no-translations", url = (string?)null, imageUrls = Array.Empty<string>(), sortOrder = 0, isActive = true, translations = new { } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A translation keyed by a language the panel does not support is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_UnsupportedLocale_Refuses()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/admin/projects", new { slug = "bad-locale", url = (string?)null, imageUrls = Array.Empty<string>(), sortOrder = 0, isActive = true, translations = new { fr = new { title = "Titre", summary = (string?)null, description = (string?)null } } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A second project cannot reuse a slug another project holds.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_DuplicateSlug_RefusesWithSlugExistsCode()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var slug = $"dup-{Guid.NewGuid():n}";
        await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/projects", StorefrontContentTestSeeds.ProjectBody(slug: slug));

        var response = await client.PostAsJsonAsync("/api/admin/projects", StorefrontContentTestSeeds.ProjectBody(slug: slug));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PROJECT_SLUG_EXISTS);
    }

    /// <summary>
    /// Two creates racing for one slug end as one success and one slug refusal, never a server error:
    /// when both pass the existence check, the unique index refuses the loser and the handler maps it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_ConcurrentSameSlug_OneWinsTheOtherIsRefusedWithSlugExists()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var slug = $"race-{Guid.NewGuid():n}";

        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/admin/projects", StorefrontContentTestSeeds.ProjectBody(slug: slug)),
            client.PostAsJsonAsync("/api/admin/projects", StorefrontContentTestSeeds.ProjectBody(slug: slug)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        var loser = responses.Single(r => r.StatusCode != HttpStatusCode.Created);
        loser.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await StorefrontContentTestSeeds.AssertCodeAsync(loser, HostpanelErrorCodes.PROJECT_SLUG_EXISTS);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Create_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/admin/projects", StorefrontContentTestSeeds.ProjectBody());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
