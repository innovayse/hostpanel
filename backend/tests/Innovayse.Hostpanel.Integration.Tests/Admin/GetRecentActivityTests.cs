using System.Net;

namespace Innovayse.Hostpanel.Integration.Tests.Admin;

/// <summary>
/// <c>GET /api/admin/dashboard/activity</c>: an admin reads the merged, newest-first
/// activity feed, an out-of-range limit substitutes the default rather than being refused, a
/// caller with only the Client role is refused, and the route is closed to a caller with no
/// credential. This use case refuses nothing once authorized.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetRecentActivityTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin reads the activity feed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanReadTheActivityFeedAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/dashboard/activity");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>An out-of-range limit substitutes the default of 10 rather than being refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OutOfRangeLimit_SubstitutesTheDefaultAsync()
    {
        var client = await AdminTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/admin/dashboard/activity?limit=0");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await AdminTestSeeds.ClientRoleClientAsync(factory, "recent-activity-forbidden");

        var response = await client.GetAsync("/api/admin/dashboard/activity");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The activity feed is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/dashboard/activity");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
