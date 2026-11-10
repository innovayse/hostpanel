using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>POST /api/reports/ssl-monitoring/revalidate</c> against Postgres: refreshes and regroups
/// the SSL monitoring report.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RevalidateSslMonitoringTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Revalidating SSL monitoring answers with the refreshed, grouped report.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnswersWithGroupedResults()
    {
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsync("/api/reports/ssl-monitoring/revalidate?includeInactive=true", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("groups", out _).Should().BeTrue();
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsync("/api/reports/ssl-monitoring/revalidate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "revalidate-ssl-monitoring");

        var response = await client.PostAsync("/api/reports/ssl-monitoring/revalidate", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
