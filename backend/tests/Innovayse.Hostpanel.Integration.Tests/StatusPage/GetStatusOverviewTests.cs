using System.Net;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Integration tests for the <c>GetStatusOverview</c> use case: <c>GET /api/public/status</c>, the public status page feed.
/// Anonymous: visitors need it exactly when they cannot sign in.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetStatusOverviewTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The feed is publicly accessible without authentication.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Overview_ReturnsOk_WithoutAuth()
    {
        var response = await StorefrontContentTestSeeds.GetPublicAsync(factory, StatusPageTestSeeds.OverviewRoute);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>The feed carries the overall state, the groups and the active incidents, and is cacheable for a short time.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Overview_CarriesTheContractShape_AndIsCacheable()
    {
        var response = await StorefrontContentTestSeeds.GetPublicAsync(factory, StatusPageTestSeeds.OverviewRoute);
        var overview = await StatusPageTestSeeds.GetOverviewAsync(factory);

        overview.TryGetProperty("overallState", out _).Should().BeTrue();
        overview.GetProperty("groups").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
        overview.GetProperty("activeIncidents").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
        response.Headers.CacheControl!.Public.Should().BeTrue();
    }

    /// <summary>Groups and their components follow the display order the operator set.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Overview_ListsComponentsInDisplayOrder()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var groupId = await StatusPageTestSeeds.CreateGroupAsync(client);
        foreach (var (name, order) in new[] { ("Second", 5), ("First", 1) })
        {
            await StorefrontContentTestSeeds.CreateAsync(
                client,
                $"{StatusPageTestSeeds.AdminRoute}/components",
                new { groupId, name, description = (string?)null, displayOrder = order });
        }

        var group = StatusPageTestSeeds.GroupOf(await StatusPageTestSeeds.GetOverviewAsync(factory), groupId);

        group.GetProperty("components").EnumerateArray().Select(c => c.GetProperty("name").GetString()).Should().Equal("First", "Second");
    }
}
