using System.Net;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>GET /api/downloads</c>: an admin can list downloads, and the list is closed to a caller
/// with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListDownloadsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can list downloads.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanListDownloadsAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/downloads");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>The download list is closed to a caller with no credential.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListIsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/downloads");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
