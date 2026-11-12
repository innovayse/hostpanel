using System.Net;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Integration tests for the <c>ListStatusMaintenance</c> use case: <c>GET /api/public/status/maintenance, the public maintenance feed. Anonymous</c>.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListStatusMaintenanceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The feed is publicly accessible without authentication and is cacheable for a short time.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ReturnsOk_WithoutAuth_AndIsCacheable()
    {
        var response = await StorefrontContentTestSeeds.GetPublicAsync(factory, StatusPageTestSeeds.MaintenanceRoute);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.Public.Should().BeTrue();
    }

    /// <summary>A window under way is listed as in progress; one that ended is left out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ShowsAWindowUnderWay_AndLeavesOutAnEndedOne()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (_, componentId) = await StatusPageTestSeeds.CreateComponentAsync(client);
        var underWay = await StatusPageTestSeeds.CreateMaintenanceAsync(client, [componentId], DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1));
        var ended = await StatusPageTestSeeds.CreateMaintenanceAsync(client, [componentId], DateTimeOffset.UtcNow.AddHours(-3), DateTimeOffset.UtcNow.AddHours(-2));

        var list = await StatusPageTestSeeds.GetMaintenanceAsync(factory);

        list.Single(m => m.GetProperty("id").GetInt32() == underWay).GetProperty("state").GetString().Should().Be("InProgress");
        list.Should().NotContain(m => m.GetProperty("id").GetInt32() == ended);
    }

    /// <summary>Windows are listed soonest start first.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_OrdersBySoonestStart()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (_, componentId) = await StatusPageTestSeeds.CreateComponentAsync(client);
        var later = await StatusPageTestSeeds.CreateMaintenanceAsync(client, [componentId], DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(6));
        var sooner = await StatusPageTestSeeds.CreateMaintenanceAsync(client, [componentId], DateTimeOffset.UtcNow.AddDays(2), DateTimeOffset.UtcNow.AddDays(3));

        var ids = (await StatusPageTestSeeds.GetMaintenanceAsync(factory)).Select(m => m.GetProperty("id").GetInt32()).ToList();

        ids.IndexOf(sooner).Should().BeLessThan(ids.IndexOf(later));
    }
}
