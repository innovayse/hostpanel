using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Integration tests for the <c>OpenStatusIncident</c> use case: <c>POST /api/admin/status/incidents</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class OpenStatusIncidentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An announced incident is open in the public overview, investigating, with its first message.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Open_ShowsTheIncidentAsActiveWithItsFirstMessage()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/incidents", new { title = "Slow panel", message = "We are looking into it." });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        var id = await response.Content.ReadFromJsonAsync<int>();
        var incident = (await StatusPageTestSeeds.GetOverviewAsync(factory))
            .GetProperty("activeIncidents").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == id);
        incident.GetProperty("title").GetString().Should().Be("Slow panel");
        incident.GetProperty("state").GetString().Should().Be("Investigating");
        incident.GetProperty("resolvedAt").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        incident.GetProperty("updates").EnumerateArray().Single().GetProperty("message").GetString().Should().Be("We are looking into it.");
    }

    /// <summary>An empty title is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Open_EmptyTitle_RefusesWithValidationError()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/incidents", new { title = "", message = "We are looking into it." });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Open_RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/incidents", new { title = "Slow panel", message = "Looking." });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
