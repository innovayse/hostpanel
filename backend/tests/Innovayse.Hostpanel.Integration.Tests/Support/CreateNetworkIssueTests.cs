using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Domain.Support.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Support;

/// <summary>
/// <c>POST /api/network-issues</c>: an admin can create a network issue, an unrecognised type is
/// refused before the domain is touched, and the action is closed to a caller with no credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateNetworkIssueTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin can create a network issue.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AdminCanCreateANetworkIssueAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/network-issues", new
        {
            title = $"Issue-{Guid.NewGuid():N}",
            type = "Server",
            server = "web-01",
            priority = nameof(NetworkIssuePriority.High),
            startDate = TestMoments.Seeded,
            description = "Something is down.",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>Creating a network issue with an unrecognised type answers 400, not a 500.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreateNetworkIssueWithBadTypeAnswersBadRequestAsync()
    {
        var client = await SupportTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync("/api/network-issues", new
        {
            title = "Broken",
            type = "NotARealType",
            server = (string?)null,
            priority = nameof(NetworkIssuePriority.High),
            startDate = TestMoments.Seeded,
            description = "Bad input.",
        });

        // A validator refusal is answered 400 VALIDATION_ERROR on the bus (rules/cqrs.md); 422 is
        // reserved for a handler's own *_VALIDATION_FAILED code.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A caller with no credential cannot create a network issue.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/network-issues", new
        {
            title = "Anything",
            type = "Server",
            server = (string?)null,
            priority = nameof(NetworkIssuePriority.High),
            startDate = TestMoments.Seeded,
            description = "Anything.",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
