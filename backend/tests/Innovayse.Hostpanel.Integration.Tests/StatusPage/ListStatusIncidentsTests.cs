using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Integration tests for the <c>ListStatusIncidents</c> use case: <c>GET /api/public/status/incidents</c>, the public
/// incident history. Anonymous: visitors need it exactly when they cannot sign in.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListStatusIncidentsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A fresh incident is in the default window, without authentication.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ReturnsRecentIncidents_WithoutAuth()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var id = await StatusPageTestSeeds.OpenIncidentAsync(client);

        var response = await StorefrontContentTestSeeds.GetPublicAsync(factory, StatusPageTestSeeds.IncidentsRoute);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        list!.Should().Contain(i => i.GetProperty("id").GetInt32() == id);
    }

    /// <summary>A window of one day still holds an incident announced just now.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_NarrowWindow_StillHoldsAFreshIncident()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var id = await StatusPageTestSeeds.OpenIncidentAsync(client);

        var response = await StorefrontContentTestSeeds.GetPublicAsync(factory, $"{StatusPageTestSeeds.IncidentsRoute}?days=1");

        var list = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        list!.Should().Contain(i => i.GetProperty("id").GetInt32() == id);
    }

    /// <summary>A window outside 1 to 365 days is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <param name="days">A window the feed refuses.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public async Task List_WindowOutOfRange_RefusesWithValidationError(int days)
    {
        var response = await StorefrontContentTestSeeds.GetPublicAsync(factory, $"{StatusPageTestSeeds.IncidentsRoute}?days={days}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }
}
