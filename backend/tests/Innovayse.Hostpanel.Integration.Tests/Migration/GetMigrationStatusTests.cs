using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Migration;

/// <summary>
/// <c>GET /api/admin/migrations/{id}</c>, dispatching <c>GetMigrationStatusQuery</c>: the current
/// status and progress of one migration job.
/// </summary>
/// <remarks>
/// Runs in CI, where Testcontainers can start PostgreSQL; on a laptop without a reachable docker
/// socket every test in this project fails before the API boots, and that is the environment, not
/// these tests.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetMigrationStatusTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A freshly created job's status is returned with the id requested.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetReturnsTheJobsStatus()
    {
        var client = await MigrationTestSeeds.AdminClientAsync(factory);
        var id = await MigrationTestSeeds.CreateJobAsync(client, "http://status.example.test");

        var response = await client.GetAsync($"/api/admin/migrations/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("id").GetInt32().Should().Be(id);
    }

    /// <summary>An id naming no job is refused as missing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetRefusesAnUnknownId()
    {
        var client = await MigrationTestSeeds.AdminClientAsync(factory);
        const int missing = 987654321;

        var response = await client.GetAsync($"/api/admin/migrations/{missing}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await MigrationTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.MIGRATION_JOB_NOT_FOUND);
    }

    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/migrations/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A non-positive job id is refused by the validator with 400 <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StatusRefusesANonPositiveId()
    {
        var client = await MigrationTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/migrations/0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await MigrationTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }
}
