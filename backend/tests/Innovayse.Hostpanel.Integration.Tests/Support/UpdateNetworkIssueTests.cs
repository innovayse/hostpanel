using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Support.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>PUT /api/network-issues/{id}</c>: an admin can update a network issue, an id names no
/// issue is refused <c>NETWORKISSUE_NOT_FOUND</c>, and the action is closed to a caller with no
/// credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpdateNetworkIssueTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The fixed moment the seeded rows and the events are stamped with.</summary>
    private static readonly DateTimeOffset SeededAt = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>An admin can update an existing network issue.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanUpdateANetworkIssueAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);
        var createResponse = await client.PostAsJsonAsync("/api/network-issues", new
        {
            title = $"Issue-{Guid.NewGuid():N}",
            type = "Server",
            server = "web-01",
            priority = nameof(NetworkIssuePriority.High),
            startDate = SeededAt,
            description = "Something is down.",
        });
        var id = int.Parse((await createResponse.Content.ReadAsStringAsync()).Trim());

        var response = await client.PutAsJsonAsync($"/api/network-issues/{id}", new
        {
            title = "Renamed",
            type = "Server",
            server = "web-02",
            priority = nameof(NetworkIssuePriority.Low),
            status = "Investigating",
            startDate = SeededAt,
            endDate = (DateTimeOffset?)null,
            description = "Updated.",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Updating a network issue that does not exist answers 404 with <c>NETWORKISSUE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UpdateUnknownNetworkIssueAnswersNotFoundAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PutAsJsonAsync($"/api/network-issues/{SupportTestSeeds.NoSuchId}", new
        {
            title = "Anything",
            type = "Server",
            server = (string?)null,
            priority = nameof(NetworkIssuePriority.High),
            status = "Reported",
            startDate = SeededAt,
            endDate = (DateTimeOffset?)null,
            description = "Anything.",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await SupportTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.NETWORKISSUE_NOT_FOUND);
    }

    /// <summary>A caller with no credential cannot update a network issue.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PutAsJsonAsync($"/api/network-issues/{SupportTestSeeds.NoSuchId}", new
        {
            title = "Anything",
            type = "Server",
            server = (string?)null,
            priority = nameof(NetworkIssuePriority.High),
            status = "Reported",
            startDate = SeededAt,
            endDate = (DateTimeOffset?)null,
            description = "Anything.",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
