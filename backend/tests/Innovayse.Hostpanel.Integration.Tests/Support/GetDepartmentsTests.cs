using System.Net;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// The department list: reachable with no credential at all on <c>GET /api/departments</c> (the
/// ticket form needs it to populate its department picker before the visitor has any admin
/// context), and listed for staff on <c>GET /api/tickets/departments</c>, which stays closed to
/// callers with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetDepartmentsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The department list is reachable with no credential at all.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListIsReachableAnonymouslyAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/departments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Staff can list the departments through <c>/api/tickets/departments</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StaffCanListDepartmentsAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        await SupportTestSeeds.CreateDepartmentAsync(client);

        var response = await client.GetAsync("/api/tickets/departments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A caller with no credential cannot list departments through the ticket screens' route.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StaffRouteIsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/tickets/departments");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
