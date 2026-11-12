using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Integration tests for the <c>UpdateStatusIncident</c> use case: <c>PUT /api/admin/status/incidents/{id}</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateStatusIncidentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The new headline shows in the public incident list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_ShowsTheNewTitle()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var id = await StatusPageTestSeeds.OpenIncidentAsync(client);

        var response = await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/incidents/{id}", new { title = "Panel latency" });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = (await (await StorefrontContentTestSeeds.GetPublicAsync(factory, StatusPageTestSeeds.IncidentsRoute)).Content.ReadFromJsonAsync<JsonElement[]>())!;
        list.Single(i => i.GetProperty("id").GetInt32() == id).GetProperty("title").GetString().Should().Be("Panel latency");
    }

    /// <summary>An empty title is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_EmptyTitle_RefusesWithValidationError()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var id = await StatusPageTestSeeds.OpenIncidentAsync(client);

        var response = await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/incidents/{id}", new { title = " " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>An id that names no incident answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_UnknownId_RefusesWithNotFoundCode()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/incidents/2147483000", new { title = "Panel latency" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.STATUS_INCIDENT_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Update_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/incidents/1", new { title = "Panel latency" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
