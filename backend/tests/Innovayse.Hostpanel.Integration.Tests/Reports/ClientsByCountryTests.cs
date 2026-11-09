using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/clients-by-country</c> against Postgres: client counts and paid-invoice
/// revenue grouped by country.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ClientsByCountryTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Country counts include a freshly created client's country.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IncludesANewClient()
    {
        var marker = $"Country{Guid.NewGuid():N}";
        await ReportsTestSeeds.CreateClientAsync(factory, marker, "Springfield", "US");

        var client = await ReportsTestSeeds.AdminClientAsync(factory);
        var response = await client.GetAsync("/api/reports/clients-by-country");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var rows = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        rows!.Should().Contain(r => r.GetProperty("country").GetString() == "US");
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/clients-by-country");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "clients-by-country");

        var response = await client.GetAsync("/api/reports/clients-by-country");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
