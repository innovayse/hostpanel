using System.Net;
using Innovayse.Hostpanel.Domain.Support.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>GET /api/network-issues</c>: an admin can list network issues, and the list is closed to a
/// caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListNetworkIssuesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can list network issues.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanListNetworkIssuesAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/network-issues?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>The network issue list is closed to a caller with no credential.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListIsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/network-issues?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A status filter given by name, repeated to cover several statuses, is accepted.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task KnownStatusFiltersAreAcceptedAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync($"/api/network-issues?page=1&pageSize=20&statuses={nameof(NetworkIssueStatus.Reported)}&statuses={nameof(NetworkIssueStatus.Investigating)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A status filter that names no network issue status is refused with 400, not silently ignored.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownStatusFilterIsRejectedAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.GetAsync("/api/network-issues?page=1&pageSize=20&statuses=Archived");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
