using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>DELETE /api/admin/plugins/registry/{id}/licence</c>: a clear deletes the stored key row and the details page then
/// reports no key, a clear with no key stored succeeds and records nothing, the clear is audited without the key, an id
/// of the wrong shape is refused, and the route is closed to callers who are not administrators.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ClearRegistryPluginLicenceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route of the licence key.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The route.</returns>
    private static string Route(string pluginId) => $"/api/admin/plugins/registry/{pluginId}/licence";

    /// <summary>Counts the rows of a plugin's licence setting straight from the table.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>How many rows hold the key.</returns>
    private async Task<int> RowCountAsync(string pluginId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var key = PluginSettingKeys.RegistryLicenceKey(pluginId);
        return await db.Settings.AsNoTracking().CountAsync(s => s.Key == key);
    }

    /// <summary>A clear deletes the row and the details page then reports no key and sends none.</summary>
    [Fact]
    public async Task AClear_DeletesTheKeyAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-lic-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", [1], pricing: PluginRegistryPricing.Paid);
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);
        await client.PutAsJsonAsync(Route(pluginId), new { key = "to-be-cleared" });
        (await RowCountAsync(pluginId)).Should().Be(1);

        var response = await client.DeleteAsync(Route(pluginId));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await RowCountAsync(pluginId)).Should().Be(0);
        var details = await client.GetFromJsonAsync<JsonElement>($"/api/admin/plugins/registry/{pluginId}");
        details.GetProperty("hasLicenceKey").GetBoolean().Should().BeFalse();
        registry.LastLicenceKey.Should().BeNull();
    }

    /// <summary>Clearing a key that is not stored succeeds and writes nothing to the activity log.</summary>
    [Fact]
    public async Task AClearWithNoKey_SucceedsAndRecordsNothingAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-lic-{Guid.NewGuid():N}";
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.DeleteAsync(Route(pluginId));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ActivityLogs.AsNoTracking().AnyAsync(l => l.Description.Contains(pluginId))).Should().BeFalse();
    }

    /// <summary>A save and a clear are written to the activity log with the plugin id and never the key.</summary>
    [Fact]
    public async Task SaveAndClear_AreAuditedWithoutTheKeyAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-lic-{Guid.NewGuid():N}";
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);
        const string key = "LICENCE-KEY-audit-secret";

        await client.PutAsJsonAsync(Route(pluginId), new { key });
        await client.DeleteAsync(Route(pluginId));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lines = await db.ActivityLogs.AsNoTracking()
            .Where(l => l.Description.Contains(pluginId))
            .OrderBy(l => l.CreatedAt)
            .Select(l => new { l.ClientId, l.Description })
            .ToListAsync();
        lines.Select(l => l.Description).Should().Equal(
            $"Saved Plugin Licence Key - Plugin ID: {pluginId}",
            $"Cleared Plugin Licence Key - Plugin ID: {pluginId}");
        lines.Should().OnlyContain(l => l.ClientId == null && !l.Description.Contains(key));
    }

    /// <summary>An id of the wrong shape is refused.</summary>
    /// <param name="pluginId">The route id.</param>
    [Theory]
    [InlineData("Bad_Id")]
    [InlineData("UPPER")]
    public async Task AMalformedId_IsRefusedAsync(string pluginId)
    {
        var registry = new FakePluginRegistryClient();
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.DeleteAsync(Route(pluginId));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A caller with only the Client role is refused with 403 and the key is kept.</summary>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var admin = await PluginRegistryTestSeeds.AdminClientAsync(factory, new FakePluginRegistryClient());
        var client = await PluginsTestSeeds.ClientRoleClientAsync(factory, "clear-plugin-licence-forbidden");
        var pluginId = $"test-lic-{Guid.NewGuid():N}";
        await admin.PutAsJsonAsync(Route(pluginId), new { key = "kept" });

        try
        {
            var response = await client.DeleteAsync(Route(pluginId));

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await RowCountAsync(pluginId)).Should().Be(1);
        }
        finally
        {
            await admin.DeleteAsync(Route(pluginId));
        }
    }

    /// <summary>The route is not for anonymous callers.</summary>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().DeleteAsync(Route($"test-lic-{Guid.NewGuid():N}"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
