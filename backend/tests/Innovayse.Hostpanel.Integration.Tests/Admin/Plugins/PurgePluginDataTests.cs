using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Plugins;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Contracts.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>DELETE /api/admin/plugins/{id}/data</c>: after a plugin is removed its data is still kept and the plugin is listed
/// as removed; the operator's purge deletes the data in every scope, writes an activity line and leaves other plugins
/// alone; a purge is refused while the plugin is installed (<c>PLUGIN_STILL_INSTALLED</c>), answers
/// <c>PLUGIN_NOT_FOUND</c> when the plugin keeps nothing, and is closed to the Client role and to anonymous callers.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PurgePluginDataTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>How long a purge may take to settle; generous because the first delivery compiles the handler chains.</summary>
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(60);

    /// <summary>A quota no seed reaches: the seeds fill the scope totals, they do not test the quota.</summary>
    private static readonly PluginStoreQuota Unbounded = new(int.MaxValue, long.MaxValue);

    /// <summary>
    /// Remove then purge: the removal keeps the data and lists the plugin as removed, the purge deletes the plugin's
    /// entries and scope rows, records who purged them, and leaves another plugin's rows alone.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RemoveThenPurge_KeepsThenDeletesTheDataAndAuditsItAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        var otherPluginId = $"test-plugin-{Guid.NewGuid():N}";
        (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(pluginId))).StatusCode.Should().Be(HttpStatusCode.OK);
        await SeedStoreRowsAsync(pluginId, otherPluginId);
        (await client.DeleteAsync($"/api/admin/plugins/{pluginId}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await EntryCountAsync(pluginId)).Should().Be(2, "the removal kept the data");

        var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/plugins");
        list!.Single(p => p.GetProperty("id").GetString() == pluginId).GetProperty("status").GetString()
            .Should().Be("Removed", "a removed plugin that keeps data is listed so the data can be purged");

        var purge = await PurgeAndSettleAsync(client, pluginId);

        purge.StatusCode.Should().Be(HttpStatusCode.OK);
        (await EntryCountAsync(pluginId)).Should().Be(0);
        (await ScopeCountAsync(pluginId)).Should().Be(0);
        (await EntryCountAsync(otherPluginId)).Should().Be(2, "another plugin's entries are not touched");
        (await ScopeCountAsync(otherPluginId)).Should().Be(2);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var line = await db.ActivityLogs.AsNoTracking()
            .SingleAsync(l => l.Description.StartsWith("Purged Plugin Data") && l.Description.Contains(pluginId));
        line.ClientId.Should().BeNull("a plugin purge concerns no client");
        var after = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/plugins");
        after!.Should().NotContain(p => p.GetProperty("id").GetString() == pluginId, "nothing of the plugin is left to list");
    }

    /// <summary>A plugin whose package is still installed is refused with 409 and keeps its data.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WhileInstalled_RefusesAndKeepsTheDataAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(pluginId))).StatusCode.Should().Be(HttpStatusCode.OK);
        await SeedStoreRowsAsync(pluginId);

        try
        {
            var response = await PurgeAndSettleAsync(client, pluginId);

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_STILL_INSTALLED);
            (await EntryCountAsync(pluginId)).Should().Be(2);
            (await ScopeCountAsync(pluginId)).Should().Be(2);
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>
    /// A package folder whose <c>plugin.json</c> is corrupt is still installed: it is not in the list of readable
    /// manifests, but the purge looks at the folder itself and refuses, keeping the data.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CorruptManifestFolder_StillCountsAsInstalledAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        var folder = PluginsTestSeeds.InstalledFolder(factory, pluginId);
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "plugin.json"), "{ this is not json");
        await SeedStoreRowsAsync(pluginId);

        try
        {
            var response = await PurgeAndSettleAsync(client, pluginId);

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_STILL_INSTALLED);
            (await EntryCountAsync(pluginId)).Should().Be(2);
            var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/plugins");
            list!.Should().NotContain(
                p => p.GetProperty("id").GetString() == pluginId && p.GetProperty("status").GetString() == "Removed",
                "a folder on disk is not a removed plugin");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// A removed plugin whose only leftover is its slot widget bindings is listed as removed and can be purged: the
    /// bindings count as kept data, and the purge deletes them.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OnlyBindingsKept_ListsTheRemovedPluginAndPurgesThemAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        await SeedBindingAsync(pluginId);

        var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/plugins");
        list!.Single(p => p.GetProperty("id").GetString() == pluginId).GetProperty("status").GetString().Should().Be("Removed");

        var purge = await PurgeAndSettleAsync(client, pluginId);

        purge.StatusCode.Should().Be(HttpStatusCode.OK);
        (await BindingCountAsync(pluginId)).Should().Be(0);
    }

    /// <summary>Purging twice is safe: the second purge finds nothing kept, answers <c>PLUGIN_NOT_FOUND</c> and audits nothing more.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RepeatedPurge_IsIdempotentAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        await SeedStoreRowsAsync(pluginId);

        (await PurgeAndSettleAsync(client, pluginId)).StatusCode.Should().Be(HttpStatusCode.OK);
        var second = await PurgeAndSettleAsync(client, pluginId);

        second.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await PluginsTestSeeds.AssertCodeAsync(second, HostpanelErrorCodes.PLUGIN_NOT_FOUND);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ActivityLogs.AsNoTracking().CountAsync(l => l.Description.StartsWith("Purged Plugin Data") && l.Description.Contains(pluginId)))
            .Should().Be(1);
        (await EntryCountAsync(pluginId)).Should().Be(0);
    }

    /// <summary>A plugin that keeps no data answers 404 <c>PLUGIN_NOT_FOUND</c> and writes nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PluginWithoutData_AnswersNotFoundAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var pluginId = $"never-installed-{Guid.NewGuid():N}";

        var response = await PurgeAndSettleAsync(client, pluginId);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_NOT_FOUND);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ActivityLogs.AsNoTracking().CountAsync(l => l.Description.Contains(pluginId))).Should().Be(0);
    }

    /// <summary>An id breaking the plugin id rule is refused by the validator with <c>VALIDATION_ERROR</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnIdBreakingTheRule_RefusesAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync("/api/admin/plugins/Not_A.Valid-Id/data");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await PluginsTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await PluginsTestSeeds.ClientRoleClientAsync(factory, "purge-plugin-forbidden");

        var response = await client.DeleteAsync($"/api/admin/plugins/never-installed-{Guid.NewGuid():N}/data");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().DeleteAsync($"/api/admin/plugins/never-installed-{Guid.NewGuid():N}/data");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Sends the purge and waits until every message it set off has been handled. Recurring jobs are ignored, because
    /// their schedule runs beside every test and is not this test's outcome.
    /// </summary>
    /// <param name="client">The signed-in HTTP client.</param>
    /// <param name="pluginId">The plugin whose data is purged.</param>
    /// <returns>The purge's response.</returns>
    private async Task<HttpResponseMessage> PurgeAndSettleAsync(HttpClient client, string pluginId)
    {
        HttpResponseMessage? response = null;
        await factory.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(SettleTimeout)
            .IgnoreMessagesMatchingType(type => typeof(IRecurringJob).IsAssignableFrom(type))
            .ExecuteAndWaitAsync(async Task (IMessageContext _) =>
            {
                response = await client.DeleteAsync($"/api/admin/plugins/{pluginId}/data");
            });

        return response!;
    }

    /// <summary>
    /// Writes, for each given plugin, an enabled switch row, two store entries and their two scope rows: one plugin
    /// wide (no client reference) and one in a client scope.
    /// </summary>
    /// <param name="pluginIds">The plugins to seed rows for.</param>
    /// <returns>A task that completes when the rows are saved.</returns>
    private async Task SeedStoreRowsAsync(params string[] pluginIds)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var pluginId in pluginIds)
        {
            db.Settings.Add(Setting.Create(IntegrationSettingKeys.EnabledKey(pluginId), IntegrationSettingKeys.EnabledValue, null));
            AddAdmitted(db, pluginId, null, "wide-key", "wide");
            AddAdmitted(db, pluginId, "client-ref-1", "client-key", "client");
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Stages one entry and the scope row that counts it, admitted as the plugin store admits a new key.</summary>
    /// <param name="db">The context the rows are staged on.</param>
    /// <param name="pluginId">The plugin the entry belongs to.</param>
    /// <param name="clientRef">The scope's client reference, or <see langword="null"/> for the plugin-wide scope.</param>
    /// <param name="key">The entry's key.</param>
    /// <param name="value">The entry's value.</param>
    private static void AddAdmitted(AppDbContext db, string pluginId, string? clientRef, string key, string value)
    {
        var dataScope = PluginDataScope.Open(pluginId, clientRef);
        dataScope.Admit(1, PluginDataEntry.CharsOf(key, value), Unbounded);
        db.PluginDataScopes.Add(dataScope);
        db.PluginDataEntries.Add(PluginDataEntry.Create(pluginId, clientRef, key, value));
    }

    /// <summary>Stores one slot widget product binding for a plugin, as an operator's binding save would.</summary>
    /// <param name="pluginId">The plugin the binding belongs to.</param>
    /// <returns>A task that completes when the row is saved.</returns>
    private async Task SeedBindingAsync(string pluginId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.PluginSlotProductBindings.Add(PluginSlotProductBinding.Create(pluginId, "client.dashboard.cards", "summary", 1));
        await db.SaveChangesAsync();
    }

    /// <summary>Counts the slot widget product bindings kept for one plugin.</summary>
    /// <param name="pluginId">The plugin whose bindings are counted.</param>
    /// <returns>The number of binding rows stored under that plugin id.</returns>
    private async Task<int> BindingCountAsync(string pluginId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PluginSlotProductBindings.AsNoTracking().CountAsync(row => row.PluginId == pluginId);
    }

    /// <summary>Counts the plugin store entries kept for one plugin across all scopes.</summary>
    /// <param name="pluginId">The plugin whose entries are counted.</param>
    /// <returns>The number of entries stored under that plugin id.</returns>
    private async Task<int> EntryCountAsync(string pluginId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PluginDataEntries.AsNoTracking().CountAsync(row => row.PluginId == pluginId);
    }

    /// <summary>Counts the plugin store scope rows kept for one plugin.</summary>
    /// <param name="pluginId">The plugin whose scope rows are counted.</param>
    /// <returns>The number of scope rows stored under that plugin id.</returns>
    private async Task<int> ScopeCountAsync(string pluginId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PluginDataScopes.AsNoTracking().CountAsync(row => row.PluginId == pluginId);
    }
}
