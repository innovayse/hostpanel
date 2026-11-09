using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Projects;

/// <summary>
/// Integration tests for the <c>UpdateProject</c> use case: <c>PUT /api/admin/projects/{id}</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateProjectTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Updating replaces the fields and the whole translation set: a locale left out is removed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_ReplacesFieldsAndTranslations()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var id = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/projects", StorefrontContentTestSeeds.ProjectBody());

        var response = await client.PutAsJsonAsync($"/api/admin/projects/{id}", new { slug = $"edited-{Guid.NewGuid():n}", url = (string?)null, imageUrls = new[] { "/uploads/branding/content/seed/b.png" }, sortOrder = 9, isActive = false, translations = new { en = new { title = "Edited title", summary = (string?)null, description = (string?)null } } });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/admin/projects/{id}");
        fetched.GetProperty("imageUrls").EnumerateArray().Should().ContainSingle();
        fetched.GetProperty("sortOrder").GetInt32().Should().Be(9);
        fetched.GetProperty("isActive").GetBoolean().Should().BeFalse();
        var translations = fetched.GetProperty("translations");
        translations.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("en");
        translations.GetProperty("en").GetProperty("title").GetString().Should().StartWith("Edited");
    }

    /// <summary>An id that names no project answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_UnknownId_RefusesWithNotFoundCode()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync("/api/admin/projects/2147483000", new { slug = $"edited-{Guid.NewGuid():n}", url = (string?)null, imageUrls = new[] { "/uploads/branding/content/seed/b.png" }, sortOrder = 9, isActive = false, translations = new { en = new { title = "Edited title", summary = (string?)null, description = (string?)null } } });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PROJECT_NOT_FOUND);
    }

    /// <summary>Updating a project to another project's slug is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_SlugOfAnotherProject_RefusesWithSlugExistsCode()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var taken = $"taken-{Guid.NewGuid():n}";
        await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/projects", StorefrontContentTestSeeds.ProjectBody(slug: taken));
        var id = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/projects", StorefrontContentTestSeeds.ProjectBody());

        var response = await client.PutAsJsonAsync($"/api/admin/projects/{id}", new
        {
            slug = taken, url = (string?)null, imageUrls = Array.Empty<string>(), sortOrder = 0, isActive = true,
            translations = new { en = new { title = "T", summary = (string?)null, description = (string?)null } },
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PROJECT_SLUG_EXISTS);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync("/api/admin/projects/1", new { slug = $"edited-{Guid.NewGuid():n}", url = (string?)null, imageUrls = new[] { "/uploads/branding/content/seed/b.png" }, sortOrder = 9, isActive = false, translations = new { en = new { title = "Edited title", summary = (string?)null, description = (string?)null } } });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A negative sort order is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_NegativeSortOrder_RefusesWithValidationError()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var id = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/projects", StorefrontContentTestSeeds.ProjectBody());

        var response = await client.PutAsJsonAsync($"/api/admin/projects/{id}", StorefrontContentTestSeeds.ProjectBody(sortOrder: -1));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }
}
