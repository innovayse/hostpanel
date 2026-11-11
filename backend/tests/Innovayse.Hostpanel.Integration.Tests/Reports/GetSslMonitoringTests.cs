using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>GET /api/reports/ssl-monitoring</c> against Postgres: the cached SSL monitoring report.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetSslMonitoringTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The cached SSL monitoring report answers even with no domains configured.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnswersWithTheCache()
    {
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/reports/ssl-monitoring");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("groups", out _).Should().BeTrue();
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/reports/ssl-monitoring");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "ssl-monitoring");

        var response = await client.GetAsync("/api/reports/ssl-monitoring");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
