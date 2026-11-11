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
