using System.Net;
using System.Net.Http.Json;

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

    /// <summary>
    /// A component a maintenance window that is under way affects shows as under maintenance, unless an operator forced a
    /// state onto it; a component of a window still to come is untouched.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Overview_ShowsMaintenance_ForAComponentOfAWindowUnderWay()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (groupId, underWay) = await StatusPageTestSeeds.CreateComponentAsync(client);
        var forced = await StorefrontContentTestSeeds.CreateAsync(
            client, $"{StatusPageTestSeeds.AdminRoute}/components", new { groupId, name = "Forced", description = (string?)null, displayOrder = 1 });
        var upcoming = await StorefrontContentTestSeeds.CreateAsync(
            client, $"{StatusPageTestSeeds.AdminRoute}/components", new { groupId, name = "Upcoming", description = (string?)null, displayOrder = 2 });
        await client.PutAsJsonAsync($"{StatusPageTestSeeds.AdminRoute}/components/{forced}/override", new { state = "Degraded" });
        await StatusPageTestSeeds.CreateMaintenanceAsync(client, [underWay, forced], DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1));
        await StatusPageTestSeeds.CreateMaintenanceAsync(client, [upcoming], DateTimeOffset.UtcNow.AddDays(1), DateTimeOffset.UtcNow.AddDays(2));

        var components = StatusPageTestSeeds.GroupOf(await StatusPageTestSeeds.GetOverviewAsync(factory), groupId)
            .GetProperty("components").EnumerateArray()
            .ToDictionary(c => c.GetProperty("id").GetInt32(), c => c.GetProperty("state").GetString());

        components[underWay].Should().Be("Maintenance");
        components[forced].Should().Be("Degraded");
        components[upcoming].Should().Be("Operational");
    }
}
