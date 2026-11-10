using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Integration tests for the <c>PostStatusIncidentUpdate</c> use case: <c>POST /api/admin/status/incidents/{id}/updates</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PostStatusIncidentUpdateTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An update moves the incident on and is the newest entry of its timeline.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Post_MovesTheIncidentOnAndListsTheNewestEntryFirst()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var id = await StatusPageTestSeeds.OpenIncidentAsync(client);

        var response = await client.PostAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/incidents/{id}/updates", new { state = "Identified", message = "A node is down." });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var incident = (await StatusPageTestSeeds.GetOverviewAsync(factory))
            .GetProperty("activeIncidents").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == id);
        incident.GetProperty("state").GetString().Should().Be("Identified");
        var updates = incident.GetProperty("updates").EnumerateArray().ToList();
        updates.Should().HaveCount(2);
        updates[0].GetProperty("message").GetString().Should().Be("A node is down.");
    }

    /// <summary>Resolving closes the incident: it leaves the active list and carries its resolution moment.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Post_Resolved_ClosesTheIncident()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var id = await StatusPageTestSeeds.OpenIncidentAsync(client);

        var response = await client.PostAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/incidents/{id}/updates", new { state = "Resolved", message = "All good." });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusPageTestSeeds.GetOverviewAsync(factory))
            .GetProperty("activeIncidents").EnumerateArray().Should().NotContain(i => i.GetProperty("id").GetInt32() == id);
        var listed = (await client.GetFromJsonAsync<System.Text.Json.JsonElement[]>(StatusPageTestSeeds.IncidentsRoute))!
            .Single(i => i.GetProperty("id").GetInt32() == id);
        listed.GetProperty("state").GetString().Should().Be("Resolved");
        listed.GetProperty("resolvedAt").ValueKind.Should().Be(System.Text.Json.JsonValueKind.String);
    }

    /// <summary>A resolved incident takes no more updates: 409 with the already-resolved code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Post_AfterResolved_RefusesWithConflictCode()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var id = await StatusPageTestSeeds.OpenIncidentAsync(client);
        (await client.PostAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/incidents/{id}/updates", new { state = "Resolved", message = "All good." }))
            .EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/incidents/{id}/updates", new { state = "Monitoring", message = "Again." });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.STATUS_INCIDENT_ALREADY_RESOLVED);
    }

    /// <summary>An id that names no incident answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Post_UnknownId_RefusesWithNotFoundCode()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/incidents/2147483000/updates", new { state = "Monitoring", message = "Again." });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.STATUS_INCIDENT_NOT_FOUND);
    }

    /// <summary>An empty message is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Post_EmptyMessage_RefusesWithValidationError()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var id = await StatusPageTestSeeds.OpenIncidentAsync(client);

        var response = await client.PostAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/incidents/{id}/updates", new { state = "Monitoring", message = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Post_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/incidents/1/updates", new { state = "Monitoring", message = "Again." });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
