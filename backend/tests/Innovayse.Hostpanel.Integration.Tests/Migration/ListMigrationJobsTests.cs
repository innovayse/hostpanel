using System.Net;

namespace Innovayse.Hostpanel.Integration.Tests.Migration;

/// <summary>
/// <c>GET /api/admin/migrations</c>, dispatching <c>ListMigrationJobsQuery</c>: every migration
/// job, most recently created first. Its happy path is exercised alongside
/// <see cref="CreateMigrationJobTests"/>, which reads the list back after creating: the content
/// the test's name actually asserts on.
/// </summary>
/// <remarks>
/// Runs in CI, where Testcontainers can start PostgreSQL; on a laptop without a reachable docker
/// socket every test in this project fails before the API boots, and that is the environment, not
/// these tests.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListMigrationJobsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/migrations");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
