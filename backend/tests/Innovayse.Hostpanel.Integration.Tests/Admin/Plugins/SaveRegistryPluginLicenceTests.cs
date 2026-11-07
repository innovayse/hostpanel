using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>PUT /api/admin/plugins/registry/{id}/licence</c>: the key is stored as an encrypted secret setting, replaced by a
/// second save, never returned by any read (the details page answers only <c>hasLicenceKey</c> and the settings list
/// does not carry it), audited without the key, refused when empty, over long or for an id of the wrong shape, and
/// closed to callers who are not administrators.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SaveRegistryPluginLicenceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route of the licence key.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The route.</returns>
    private static string Route(string pluginId) => $"/api/admin/plugins/registry/{pluginId}/licence";

    /// <summary>Reads the stored value of a plugin's licence setting straight from the table.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The stored value, or <see langword="null"/> when there is no row.</returns>
    private async Task<string?> StoredAsync(string pluginId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var key = PluginSettingKeys.RegistryLicenceKey(pluginId);
        return await db.Settings.AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).SingleOrDefaultAsync();
    }

    /// <summary>A saved key is stored encrypted, decrypts to what was sent and is not returned.</summary>
    [Fact]
    public async Task ASavedKey_IsStoredEncryptedAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-lic-{Guid.NewGuid():N}";
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);
        const string key = "LICENCE-KEY-0a1b2c3d";

        try
        {
            var response = await client.PutAsJsonAsync(Route(pluginId), new { key });

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
            var stored = await StoredAsync(pluginId);
            stored.Should().NotBeNullOrEmpty().And.NotContain(key);
            using var scope = factory.Services.CreateScope();
            var cipher = scope.ServiceProvider.GetRequiredService<ISecretSettingCipher>();
            cipher.IsProtected(stored!).Should().BeTrue();
            cipher.Reveal(stored!).Should().Be(key);
        }
        finally
        {
            await client.DeleteAsync(Route(pluginId));
        }
    }

    /// <summary>A second save replaces the first and the key is trimmed.</summary>
    [Fact]
    public async Task ASecondSave_ReplacesTheKeyAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-lic-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", [1]);
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        try
        {
            await client.PutAsJsonAsync(Route(pluginId), new { key = "first-key" });
            await client.PutAsJsonAsync(Route(pluginId), new { key = "  second-key  " });

            (await client.GetAsync($"/api/admin/plugins/registry/{pluginId}")).StatusCode.Should().Be(HttpStatusCode.OK);

            registry.LastLicenceKey.Should().Be("second-key");
            using var scope = factory.Services.CreateScope();
            var rows = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Settings.AsNoTracking()
                .CountAsync(s => s.Key == PluginSettingKeys.RegistryLicenceKey(pluginId));
            rows.Should().Be(1);
        }
        finally
        {
            await client.DeleteAsync(Route(pluginId));
        }
    }

    /// <summary>The key is a secret setting: the settings list does not carry it and the secret registry names it.</summary>
    [Fact]
    public async Task TheKey_IsASecretSettingThatNoReadReturnsAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-lic-{Guid.NewGuid():N}";
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);
        const string key = "LICENCE-KEY-do-not-leak";

        try
        {
            await client.PutAsJsonAsync(Route(pluginId), new { key });

            SecretSettingKeys.IsSecret(PluginSettingKeys.RegistryLicenceKey(pluginId)).Should().BeTrue();
            var list = await client.GetStringAsync("/api/admin/settings");
            list.Should().NotContain(key).And.NotContain(PluginSettingKeys.RegistryLicencePrefix);
        }
        finally
        {
            await client.DeleteAsync(Route(pluginId));
        }
    }

    /// <summary>A save is written to the activity log with the plugin id and never the key.</summary>
    [Fact]
    public async Task ASave_IsAuditedWithoutTheKeyAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-lic-{Guid.NewGuid():N}";
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);
        const string key = "LICENCE-KEY-audit-secret";

        try
        {
            await client.PutAsJsonAsync(Route(pluginId), new { key });

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var lines = await db.ActivityLogs.AsNoTracking()
                .Where(l => l.Description.Contains(pluginId))
                .Select(l => new { l.ClientId, l.Description })
                .ToListAsync();
            lines.Select(l => l.Description).Should().Equal($"Saved Plugin Licence Key - Plugin ID: {pluginId}");
            lines.Should().OnlyContain(l => l.ClientId == null && !l.Description.Contains(key));
        }
        finally
        {
            await client.DeleteAsync(Route(pluginId));
        }
    }

    /// <summary>An empty, blank or missing key is refused and nothing is stored.</summary>
    /// <param name="key">The key sent.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task AnEmptyKey_IsRefusedAsync(string? key)
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-lic-{Guid.NewGuid():N}";
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.PutAsJsonAsync(Route(pluginId), new { key });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await StoredAsync(pluginId)).Should().BeNull();
    }

    /// <summary>A key over the length limit is refused and nothing is stored.</summary>
    [Fact]
    public async Task AnOverLongKey_IsRefusedAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-lic-{Guid.NewGuid():N}";
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.PutAsJsonAsync(Route(pluginId), new { key = new string('k', 513) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await StoredAsync(pluginId)).Should().BeNull();
    }

    /// <summary>An id of the wrong shape is refused and nothing is stored.</summary>
    /// <param name="pluginId">The route id.</param>
    [Theory]
    [InlineData("Bad_Id")]
    [InlineData("UPPER")]
    public async Task AMalformedId_IsRefusedAsync(string pluginId)
    {
        var registry = new FakePluginRegistryClient();
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        (await client.PutAsJsonAsync(Route(pluginId), new { key = "k" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await StoredAsync(pluginId)).Should().BeNull();
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await PluginsTestSeeds.ClientRoleClientAsync(factory, "plugin-licence-forbidden");
        var pluginId = $"test-lic-{Guid.NewGuid():N}";

        (await client.PutAsJsonAsync(Route(pluginId), new { key = "k" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await StoredAsync(pluginId)).Should().BeNull();
    }

    /// <summary>The route is not for anonymous callers.</summary>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var anonymous = factory.CreateClient();
        var pluginId = $"test-lic-{Guid.NewGuid():N}";

        (await anonymous.PutAsJsonAsync(Route(pluginId), new { key = "k" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
