using System.Net;

namespace Innovayse.Hostpanel.Integration.Tests.Reports;

/// <summary>
/// <c>POST /api/reports/disk-usage/update</c> against Postgres: refreshes the cached disk usage
/// report.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateDiskUsageTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Refreshing disk usage with no control panel servers configured still answers 200.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefreshesTheCache()
    {
        var client = await ReportsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsync("/api/reports/disk-usage/update", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsync("/api/reports/disk-usage/update", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A client-role caller, not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesTheClientRole()
    {
        var client = await ReportsTestSeeds.ClientRoleClientAsync(factory, "update-disk-usage");

        var response = await client.PostAsync("/api/reports/disk-usage/update", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
