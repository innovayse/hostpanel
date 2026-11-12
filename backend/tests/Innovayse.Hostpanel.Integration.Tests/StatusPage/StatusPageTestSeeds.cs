using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.StatusPage;

/// <summary>
/// Shared helpers for the status page module's per-use-case Integration tests: the records every test starts
/// from, created through the admin API, and reading the public feeds back. Signing in, reading a refusal's code
/// and the anonymous request are the storefront content helpers, which this module reuses.
/// </summary>
internal static class StatusPageTestSeeds
{
    /// <summary>The admin route every status page management call goes through.</summary>
    public const string AdminRoute = "/api/admin/status";

    /// <summary>The public overview route.</summary>
    public const string OverviewRoute = "/api/public/status";

    /// <summary>The public incident list route.</summary>
    public const string IncidentsRoute = "/api/public/status/incidents";

    /// <summary>The public uptime route.</summary>
    public const string UptimeRoute = "/api/public/status/uptime";

    /// <summary>The public maintenance list route.</summary>
    public const string MaintenanceRoute = "/api/public/status/maintenance";

    /// <summary>The admin route maintenance windows are managed through.</summary>
    public const string AdminMaintenanceRoute = "/api/admin/status/maintenance";

    /// <summary>Builds the body that announces or changes a maintenance window.</summary>
    /// <param name="componentIds">The ids of the components the window affects.</param>
    /// <param name="start">When the window starts; an hour from now when omitted.</param>
    /// <param name="end">When the window ends; two hours from now when omitted.</param>
    /// <param name="title">The headline.</param>
    /// <returns>The anonymous body object.</returns>
    public static object MaintenanceBody(IReadOnlyList<int> componentIds, DateTimeOffset? start = null, DateTimeOffset? end = null, string title = "Network upgrade")
    {
        return new
        {
            title,
            description = (string?)"Short interruptions expected.",
            scheduledStart = start ?? DateTimeOffset.UtcNow.AddHours(1),
            scheduledEnd = end ?? DateTimeOffset.UtcNow.AddHours(2),
            componentIds,
        };
    }

    /// <summary>Announces a maintenance window through the admin API.</summary>
    /// <param name="client">An authenticated admin client.</param>
    /// <param name="componentIds">The ids of the components the window affects.</param>
    /// <param name="start">When the window starts; an hour from now when omitted.</param>
    /// <param name="end">When the window ends; two hours from now when omitted.</param>
    /// <returns>The new window's id.</returns>
    public static Task<int> CreateMaintenanceAsync(HttpClient client, IReadOnlyList<int> componentIds, DateTimeOffset? start = null, DateTimeOffset? end = null)
    {
        return StorefrontContentTestSeeds.CreateAsync(client, AdminMaintenanceRoute, MaintenanceBody(componentIds, start, end));
    }

    /// <summary>Reads the public maintenance list as an anonymous caller.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <returns>The windows.</returns>
    public static async Task<JsonElement[]> GetMaintenanceAsync(IntegrationTestFactory factory)
    {
        var response = await StorefrontContentTestSeeds.GetPublicAsync(factory, MaintenanceRoute);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement[]>())!;
    }

    /// <summary>Creates a group through the admin API.</summary>
    /// <param name="client">An authenticated admin client.</param>
    /// <returns>The new group's id.</returns>
    public static Task<int> CreateGroupAsync(HttpClient client)
    {
        return StorefrontContentTestSeeds.CreateAsync(client, $"{AdminRoute}/groups", new { name = "Hosting servers", displayOrder = 0 });
    }

    /// <summary>Creates a group and one component in it through the admin API.</summary>
    /// <param name="client">An authenticated admin client.</param>
    /// <returns>The new group's id and the new component's id.</returns>
    public static async Task<(int GroupId, int ComponentId)> CreateComponentAsync(HttpClient client)
    {
        var groupId = await CreateGroupAsync(client);
        var componentId = await StorefrontContentTestSeeds.CreateAsync(
            client,
            $"{AdminRoute}/components",
            new { groupId, name = "Web hosting", description = (string?)"Shared hosting nodes", displayOrder = 0 });
        return (groupId, componentId);
    }

    /// <summary>Announces an incident through the admin API.</summary>
    /// <param name="client">An authenticated admin client.</param>
    /// <returns>The new incident's id.</returns>
    public static Task<int> OpenIncidentAsync(HttpClient client)
    {
        return StorefrontContentTestSeeds.CreateAsync(client, $"{AdminRoute}/incidents", new { title = "Slow panel", message = "We are looking into it." });
    }

    /// <summary>Reads the public overview as an anonymous caller.</summary>
    /// <param name="factory">The shared API + PostgreSQL host.</param>
    /// <returns>The overview document.</returns>
    public static async Task<JsonElement> GetOverviewAsync(IntegrationTestFactory factory)
    {
        var response = await StorefrontContentTestSeeds.GetPublicAsync(factory, OverviewRoute);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>())!;
    }

    /// <summary>Finds a group in the public overview.</summary>
    /// <param name="overview">The overview document.</param>
    /// <param name="groupId">The group to find.</param>
    /// <returns>The group's element.</returns>
    public static JsonElement GroupOf(JsonElement overview, int groupId)
    {
        return overview.GetProperty("groups").EnumerateArray().Single(g => g.GetProperty("id").GetInt32() == groupId);
    }
}
