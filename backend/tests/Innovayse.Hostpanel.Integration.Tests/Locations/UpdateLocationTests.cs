using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Locations;

/// <summary>
/// Integration tests for the <c>UpdateLocation</c> use case: <c>PUT /api/admin/locations/{id}</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateLocationTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Updating replaces the fields and the whole translation set: a locale left out is removed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_ReplacesFieldsAndTranslations()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var id = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/locations", StorefrontContentTestSeeds.LocationBody());

        var response = await client.PutAsJsonAsync($"/api/admin/locations/{id}", new { countryCode = "nl", sortOrder = 9, isActive = false, translations = new { en = new { name = "Edited name", city = (string?)null, latencyNote = (string?)null } } });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/admin/locations/{id}");
        fetched.GetProperty("countryCode").GetString().Should().Be("NL");
        fetched.GetProperty("sortOrder").GetInt32().Should().Be(9);
        fetched.GetProperty("isActive").GetBoolean().Should().BeFalse();
        var translations = fetched.GetProperty("translations");
        translations.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("en");
        translations.GetProperty("en").GetProperty("name").GetString().Should().StartWith("Edited");
    }

    /// <summary>An id that names no location answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_UnknownId_RefusesWithNotFoundCode()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync("/api/admin/locations/2147483000", new { countryCode = "nl", sortOrder = 9, isActive = false, translations = new { en = new { name = "Edited name", city = (string?)null, latencyNote = (string?)null } } });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.LOCATION_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync("/api/admin/locations/1", new { countryCode = "nl", sortOrder = 9, isActive = false, translations = new { en = new { name = "Edited name", city = (string?)null, latencyNote = (string?)null } } });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A negative sort order is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_NegativeSortOrder_RefusesWithValidationError()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var id = await StorefrontContentTestSeeds.CreateAsync(client, "/api/admin/locations", StorefrontContentTestSeeds.LocationBody());

        var response = await client.PutAsJsonAsync($"/api/admin/locations/{id}", StorefrontContentTestSeeds.LocationBody(sortOrder: -1));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }
}
