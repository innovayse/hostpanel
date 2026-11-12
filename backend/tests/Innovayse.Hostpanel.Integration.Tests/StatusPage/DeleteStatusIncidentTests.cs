using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Integration tests for the <c>DeleteStatusIncident</c> use case: <c>DELETE /api/admin/status/incidents/{id}</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteStatusIncidentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A deleted incident leaves the public incident list.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_RemovesTheIncident()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var id = await StatusPageTestSeeds.OpenIncidentAsync(client);

        var response = await client.DeleteAsync($"{StatusPageTestSeeds.AdminRoute}/incidents/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var list = (await (await StorefrontContentTestSeeds.GetPublicAsync(factory, StatusPageTestSeeds.IncidentsRoute)).Content.ReadFromJsonAsync<JsonElement[]>())!;
        list.Should().NotContain(i => i.GetProperty("id").GetInt32() == id);
    }

    /// <summary>An id that names no incident answers 404 with the not-found code.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_UnknownId_RefusesWithNotFoundCode()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync($"{StatusPageTestSeeds.AdminRoute}/incidents/2147483000");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await StorefrontContentTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.STATUS_INCIDENT_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Delete_RefusesAnonymous()
    {
        var response = await factory.CreateClient().DeleteAsync($"{StatusPageTestSeeds.AdminRoute}/incidents/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
