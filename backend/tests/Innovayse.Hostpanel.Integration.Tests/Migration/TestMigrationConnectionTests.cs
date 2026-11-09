using System.Net;
using System.Net.Http.Headers;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Migration;

/// <summary>
/// <c>POST /api/admin/migrations/{id}/test-connection</c>, dispatching
/// <c>TestMigrationConnectionCommand</c>: tests the connection from this panel to the legacy
/// system's migration plugin.
/// </summary>
/// <remarks>
/// Runs in CI, where Testcontainers can start PostgreSQL; on a laptop without a reachable docker
/// socket every test in this project fails before the API boots, and that is the environment, not
/// these tests.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TestMigrationConnectionTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// A connection test against a reachable source succeeds and records the ping, run on an
    /// isolated host sharing the same seeded Postgres so the admin token stays valid there.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TestConnectionAgainstAReachableSourceSucceeds()
    {
        var token = await factory.GetAdminTokenAsync();
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, new AlwaysReachableSource());
        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var id = await MigrationTestSeeds.CreateJobAsync(client, "http://reachable.example.test");

        var response = await client.PostAsync($"/api/admin/migrations/{id}/test-connection", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Testing the connection against a source nothing answers is a 502, with no source detail.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TestConnectionAgainstAnUnreachableSourceAnswersUpstreamFailed()
    {
        var client = await MigrationTestSeeds.AdminClientAsync(factory);
        var id = await MigrationTestSeeds.CreateJobAsync(client, MigrationTestSeeds.UnreachableSourceUrl);

        var response = await client.PostAsync($"/api/admin/migrations/{id}/test-connection", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        await MigrationTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.MIGRATION_CONNECTION_UPSTREAM_FAILED);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("127.0.0.1");
    }

    /// <summary>An id naming no job is refused as missing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TestConnectionRefusesAnUnknownId()
    {
        var client = await MigrationTestSeeds.AdminClientAsync(factory);
        const int missing = 987654321;

        var response = await client.PostAsync($"/api/admin/migrations/{missing}/test-connection", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await MigrationTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.MIGRATION_JOB_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TestConnectionRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsync("/api/admin/migrations/1/test-connection", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive job id is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TestConnectionRefusesANonPositiveId()
    {
        var client = await MigrationTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsync("/api/admin/migrations/0/test-connection", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await MigrationTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }
}
