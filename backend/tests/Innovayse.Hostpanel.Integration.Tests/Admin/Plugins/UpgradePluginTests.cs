using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Plugins;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>POST /api/admin/plugins/install</c> for a plugin that is already installed: a different version is staged and the
/// installed files stay until the restart; a downgrade is refused with <c>PLUGIN_DOWNGRADE_REFUSED</c> unless forced;
/// an upgrade that fails a check is never staged; the plugin's settings and stored data are untouched; removing the
/// plugin drops what was staged.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpgradePluginTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>, IDisposable
{
    /// <summary>
    /// This test's own upload folder. Every host that starts swaps the upgrades staged under its own upload folder in,
    /// as the real application does at a restart, so sharing the default folder would let any other test class's host
    /// start-up swap a staged upgrade of this one in the middle of a test.
    /// </summary>
    private readonly string _installRoot = Path.Combine(Path.GetTempPath(), $"hostpanel-upgrade-{Guid.NewGuid():N}");

    /// <summary>The host that uses <see cref="_installRoot"/>, created on first use.</summary>
    private WebApplicationFactory<Program>? _host;

    /// <summary>A host derived from the shared one whose upload folder is <see cref="_installRoot"/>.</summary>
    private WebApplicationFactory<Program> Host =>
        _host ??= factory.WithWebHostBuilder(builder => builder.UseSetting("Plugins:InstallPath", _installRoot));

    /// <summary>Stops the host and deletes the upload folder.</summary>
    public void Dispose()
    {
        _host?.Dispose();
        if (Directory.Exists(_installRoot))
        {
            Directory.Delete(_installRoot, recursive: true);
        }
    }

    /// <summary>The folder an uploaded plugin is unpacked into under this test's upload folder.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The absolute folder path.</returns>
    private string InstalledFolder(string pluginId)
    {
        return Path.Combine(_installRoot, pluginId);
    }

    /// <summary>The folder an upgrade waits in until the next restart under this test's upload folder.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The absolute folder path.</returns>
    private string StagedFolder(string pluginId)
    {
        return Path.Combine(_installRoot, ".staged", pluginId);
    }

    /// <summary>
    /// A newer version is staged: the response names the staged version and asks for a restart, the installed files and
    /// the settings and stored data of the plugin are untouched, the list shows the staged version, and the staging is
    /// audited.
    /// </summary>
    [Fact]
    public async Task NewerVersion_IsStagedAndLeavesTheInstalledPluginAndItsDataAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory, Host);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "1.0.0"))).StatusCode.Should().Be(HttpStatusCode.OK);
        await SeedSettingAndDataAsync(pluginId);

        try
        {
            var upgrade = await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "2.0.0"));

            upgrade.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await upgrade.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("requiresRestart").GetBoolean().Should().BeTrue();
            body.GetProperty("stagedVersion").GetString().Should().Be("2.0.0");
            (await File.ReadAllTextAsync(Path.Combine(InstalledFolder(pluginId), "readme.txt")))
                .Should().Be("version 1.0.0", "the swap happens on restart");
            (await File.ReadAllTextAsync(Path.Combine(StagedFolder(pluginId), "readme.txt")))
                .Should().Be("version 2.0.0");
            (await SettingCountAsync(pluginId)).Should().Be(1, "the plugin's settings are kept");
            (await EntryCountAsync(pluginId)).Should().Be(1, "the plugin's stored data is kept");

            var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/plugins");
            var row = list!.Single(p => p.GetProperty("id").GetString() == pluginId);
            row.GetProperty("version").GetString().Should().Be("1.0.0");
            row.GetProperty("stagedVersion").GetString().Should().Be("2.0.0");

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var lines = await db.ActivityLogs.AsNoTracking()
                .Where(l => l.Description.StartsWith("Staged Plugin Upgrade") && l.Description.Contains(pluginId))
                .Select(l => l.Description).ToListAsync();
            lines.Should().ContainSingle().Which.Should().Contain("1.0.0 to 2.0.0");
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>Uploading an upgrade twice keeps only the last one staged.</summary>
    [Fact]
    public async Task SecondUpgrade_ReplacesTheStagedOneAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory, Host);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "1.0.0"))).StatusCode.Should().Be(HttpStatusCode.OK);

        try
        {
            (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "2.0.0"))).StatusCode.Should().Be(HttpStatusCode.OK);
            (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "2.1.0"))).StatusCode.Should().Be(HttpStatusCode.OK);

            (await File.ReadAllTextAsync(Path.Combine(StagedFolder(pluginId), "readme.txt"))).Should().Be("version 2.1.0");
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>An older version is refused with 409 <c>PLUGIN_DOWNGRADE_REFUSED</c> and nothing is staged.</summary>
    [Fact]
    public async Task Downgrade_IsRefusedWithoutForceAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory, Host);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "2.0.0"))).StatusCode.Should().Be(HttpStatusCode.OK);

        try
        {
            var response = await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "1.0.0"));

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_DOWNGRADE_REFUSED);
            Directory.Exists(StagedFolder(pluginId)).Should().BeFalse();
            (await File.ReadAllTextAsync(Path.Combine(InstalledFolder(pluginId), "readme.txt"))).Should().Be("version 2.0.0");
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>A forced downgrade is staged like an upgrade.</summary>
    [Fact]
    public async Task Downgrade_IsStagedWhenForcedAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory, Host);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "2.0.0"))).StatusCode.Should().Be(HttpStatusCode.OK);

        try
        {
            var response = await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "1.0.0"), force: true);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("stagedVersion").GetString().Should().Be("1.0.0");
            (await File.ReadAllTextAsync(Path.Combine(StagedFolder(pluginId), "readme.txt"))).Should().Be("version 1.0.0");
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>An upgrade that breaks a manifest rule is refused with that rule's code and is never staged.</summary>
    [Fact]
    public async Task UpgradeBreakingAManifestRule_IsNeverStagedAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory, Host);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "1.0.0"))).StatusCode.Should().Be(HttpStatusCode.OK);
        var broken = PluginsTestSeeds.BuildZip(new Dictionary<string, string>
        {
            ["plugin.json"] = PluginsTestSeeds.ManifestJson(pluginId, ",\"manifestVersion\":9")
                .Replace("\"version\": \"1.0.0\"", "\"version\": \"2.0.0\""),
        });

        try
        {
            var response = await PluginsTestSeeds.InstallAsync(client, broken);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_MANIFEST_VERSION_UNKNOWN);
            Directory.Exists(StagedFolder(pluginId)).Should().BeFalse();
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>Removing a plugin drops the upgrade staged for it, so it cannot come back at the next start.</summary>
    [Fact]
    public async Task Remove_DropsTheStagedUpgradeAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory, Host);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "1.0.0"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "2.0.0"))).StatusCode.Should().Be(HttpStatusCode.OK);

        var remove = await client.DeleteAsync($"/api/admin/plugins/{pluginId}");

        remove.StatusCode.Should().Be(HttpStatusCode.OK);
        Directory.Exists(StagedFolder(pluginId)).Should().BeFalse();
        Directory.Exists(InstalledFolder(pluginId)).Should().BeFalse();
    }

    /// <summary>A forced downgrade is audited as a forced downgrade with both versions, not as an ordinary upgrade.</summary>
    [Fact]
    public async Task ForcedDowngrade_IsAuditedExplicitlyAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory, Host);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "2.0.0"))).StatusCode.Should().Be(HttpStatusCode.OK);

        try
        {
            (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "1.0.0"), force: true))
                .StatusCode.Should().Be(HttpStatusCode.OK);

            (await LinesAsync("Forced Plugin Downgrade", pluginId)).Should().ContainSingle().Which.Should().Contain("2.0.0 to 1.0.0");
            (await LinesAsync("Staged Plugin Upgrade", pluginId)).Should().BeEmpty();
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>A refused downgrade attempt is audited too, with both versions.</summary>
    [Fact]
    public async Task RefusedDowngrade_IsAuditedAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory, Host);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "2.0.0"))).StatusCode.Should().Be(HttpStatusCode.OK);

        try
        {
            (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "1.0.0")))
                .StatusCode.Should().Be(HttpStatusCode.Conflict);

            (await LinesAsync("Refused Plugin Downgrade", pluginId)).Should().ContainSingle().Which.Should().Contain("2.0.0 to 1.0.0");
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>
    /// The downgrade check also compares with the version already staged: an upload older than the staged one is
    /// refused even though it is newer than the installed one, and a forced one replaces the staged upload.
    /// </summary>
    [Fact]
    public async Task UploadOlderThanTheStagedOne_IsADowngradeAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory, Host);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "1.0.0"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "3.0.0"))).StatusCode.Should().Be(HttpStatusCode.OK);

        try
        {
            var refused = await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "2.0.0"));

            refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
            await PluginsTestSeeds.AssertCodeAsync(refused, HostpanelErrorCodes.PLUGIN_DOWNGRADE_REFUSED);
            (await File.ReadAllTextAsync(Path.Combine(StagedFolder(pluginId), "readme.txt"))).Should().Be("version 3.0.0");

            (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "2.0.0"), force: true))
                .StatusCode.Should().Be(HttpStatusCode.OK);
            (await File.ReadAllTextAsync(Path.Combine(StagedFolder(pluginId), "readme.txt"))).Should().Be("version 2.0.0");
            (await LinesAsync("Forced Plugin Downgrade", pluginId)).Should().ContainSingle().Which.Should().Contain("3.0.0 to 2.0.0");
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>
    /// An installed version that cannot be compared (its manifest was edited by hand) is not provably older, so an
    /// upload is refused as a downgrade unless forced; it cannot slip through as a "different version".
    /// </summary>
    [Fact]
    public async Task UnparseableInstalledVersion_CountsAsADowngradeUnlessForcedAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory, Host);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "1.0.0"))).StatusCode.Should().Be(HttpStatusCode.OK);
        var manifestPath = Path.Combine(InstalledFolder(pluginId), "plugin.json");
        await File.WriteAllTextAsync(manifestPath, (await File.ReadAllTextAsync(manifestPath)).Replace("\"version\": \"1.0.0\"", "\"version\": \"latest\""));

        try
        {
            var refused = await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "2.0.0"));

            refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
            await PluginsTestSeeds.AssertCodeAsync(refused, HostpanelErrorCodes.PLUGIN_DOWNGRADE_REFUSED);
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>A version that is not a strict semantic version is refused with its own code and nothing is installed or staged.</summary>
    /// <param name="version">The version text in the uploaded manifest.</param>
    [Theory]
    [InlineData("1.0")]
    [InlineData("01.0.0")]
    [InlineData("1.0.0.0")]
    [InlineData("v1.0.0")]
    [InlineData("99999999999.0.0")]
    public async Task InvalidVersion_IsRefusedAtInstallAsync(string version)
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory, Host);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";

        var response = await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, version));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_MANIFEST_VERSION_INVALID);
        Directory.Exists(InstalledFolder(pluginId)).Should().BeFalse();
        Directory.Exists(StagedFolder(pluginId)).Should().BeFalse();
    }

    /// <summary>The same version uploaded again while an upgrade is staged replaces the staged upload and is audited as a replacement.</summary>
    [Fact]
    public async Task SameVersionAsTheStagedOne_IsAuditedAsAReplacementAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory, Host);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "1.0.0"))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "2.0.0"))).StatusCode.Should().Be(HttpStatusCode.OK);

        try
        {
            (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "2.0.0+rebuilt")))
                .StatusCode.Should().Be(HttpStatusCode.OK);

            (await LinesAsync("Staged Plugin Replacement", pluginId)).Should().ContainSingle().Which.Should().Contain("2.0.0 to 2.0.0+rebuilt");
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>Reads the activity log lines that start with a title and name a plugin.</summary>
    /// <param name="title">The start of the line.</param>
    /// <param name="pluginId">The plugin id the line must contain.</param>
    /// <returns>The descriptions.</returns>
    private async Task<List<string>> LinesAsync(string title, string pluginId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ActivityLogs.AsNoTracking()
            .Where(l => l.Description.StartsWith(title) && l.Description.Contains(pluginId))
            .Select(l => l.Description)
            .ToListAsync();
    }

    /// <summary>Stores a setting row and a store entry for the plugin.</summary>
    /// <param name="pluginId">The plugin the rows belong to.</param>
    /// <returns>A task that completes when the rows are saved.</returns>
    private async Task SeedSettingAndDataAsync(string pluginId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Settings.Add(Setting.Create(IntegrationSettingKeys.FieldKey(pluginId, "base_url"), "https://example.org", null));
        var dataScope = PluginDataScope.Open(pluginId, null);
        dataScope.Admit(1, PluginDataEntry.CharsOf("cursor", "7"), new PluginStoreQuota(int.MaxValue, long.MaxValue));
        db.PluginDataScopes.Add(dataScope);
        db.PluginDataEntries.Add(PluginDataEntry.Create(pluginId, null, "cursor", "7"));
        await db.SaveChangesAsync();
    }

    /// <summary>Counts the plugin's stored setting rows.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <returns>The number of settings rows under the plugin's prefix.</returns>
    private async Task<int> SettingCountAsync(string pluginId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var prefix = IntegrationSettingKeys.Prefix(pluginId);
        return await db.Settings.AsNoTracking().CountAsync(s => s.Key.StartsWith(prefix));
    }

    /// <summary>Counts the plugin's stored store entries.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <returns>The number of entries under the plugin id.</returns>
    private async Task<int> EntryCountAsync(string pluginId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PluginDataEntries.AsNoTracking().CountAsync(e => e.PluginId == pluginId);
    }
}
