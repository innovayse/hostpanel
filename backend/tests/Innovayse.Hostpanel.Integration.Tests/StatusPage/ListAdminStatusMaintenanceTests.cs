using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Integration tests for the <c>ListAdminStatusMaintenance</c> use case: <c>GET /api/admin/status/maintenance</c>, every window for the admin panel.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListAdminStatusMaintenanceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An anonymous caller is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ReturnsUnauthorized_WithoutAuth()
    {
        var response = await StorefrontContentTestSeeds.GetPublicAsync(factory, StatusPageTestSeeds.AdminMaintenanceRoute);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Upcoming, under way, ended and cancelled windows are all listed, newest scheduled start first.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task List_ReturnsEveryWindow_NewestStartFirst()
    {
        var client = await StorefrontContentTestSeeds.AdminClientAsync(factory);
        var (_, componentId) = await StatusPageTestSeeds.CreateComponentAsync(client);
        var upcoming = await StatusPageTestSeeds.CreateMaintenanceAsync(client, [componentId], DateTimeOffset.UtcNow.AddDays(4), DateTimeOffset.UtcNow.AddDays(5));
        var cancelled = await StatusPageTestSeeds.CreateMaintenanceAsync(client, [componentId], DateTimeOffset.UtcNow.AddDays(2), DateTimeOffset.UtcNow.AddDays(3));
        var underWay = await StatusPageTestSeeds.CreateMaintenanceAsync(client, [componentId], DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1));
        var ended = await StatusPageTestSeeds.CreateMaintenanceAsync(client, [componentId], DateTimeOffset.UtcNow.AddHours(-3), DateTimeOffset.UtcNow.AddHours(-2));
        (await client.PostAsync($"{StatusPageTestSeeds.AdminMaintenanceRoute}/{cancelled}/cancel", null)).EnsureSuccessStatusCode();

        var response = await client.GetAsync(StatusPageTestSeeds.AdminMaintenanceRoute);
        response.EnsureSuccessStatusCode();
        var list = (await response.Content.ReadFromJsonAsync<JsonElement[]>())!;
        var ids = list.Select(m => m.GetProperty("id").GetInt32()).ToList();

        ids.Should().Contain([upcoming, cancelled, underWay, ended]);
        ids.IndexOf(upcoming).Should().BeLessThan(ids.IndexOf(cancelled));
        ids.IndexOf(cancelled).Should().BeLessThan(ids.IndexOf(underWay));
        ids.IndexOf(underWay).Should().BeLessThan(ids.IndexOf(ended));
        list.Single(m => m.GetProperty("id").GetInt32() == cancelled).GetProperty("state").GetString().Should().Be("Cancelled");
        list.Single(m => m.GetProperty("id").GetInt32() == ended).GetProperty("state").GetString().Should().Be("Completed");
    }
}
