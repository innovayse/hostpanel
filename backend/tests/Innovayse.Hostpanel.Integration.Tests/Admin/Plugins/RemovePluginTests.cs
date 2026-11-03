using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Admin.Plugins.Commands.RemovePlugin;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Plugins;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>DELETE /api/admin/plugins/{id}</c>: installing then removing a plugin takes it out of the list, switches it off,
/// writes the removal to the activity log and keeps the plugin's stored entries and scope rows (a separate purge
/// deletes them, see <c>PurgePluginDataTests</c>); a valid id whose package is already gone is still switched off and
/// answers no restart when it has a switch row, and answers <c>PLUGIN_NOT_FOUND</c> and writes nothing when it never
/// had one; the switch and the audit line commit together, so a rolled-back removal neither switches off nor records;
/// a plugin shipped with the build and an id breaking the plugin id rule are refused and touch nothing; a caller with only the Client role is refused, and the route is closed to a caller with no
/// credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RemovePluginTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A quota no seed reaches: the seeds fill the scope totals, they do not test the quota.</summary>
    private static readonly PluginStoreQuota Unbounded = new(int.MaxValue, long.MaxValue);

    /// <summary>
    /// Installing then removing a plugin takes it out of the list, answers a restart, records the removal and leaves
    /// the plugin switched off.
    /// </summary>
    [Fact]
    public async Task InstallThenRemove_TakesItOutOfTheListAndRecordsItAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";

        var install = await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(pluginId));
        install.StatusCode.Should().Be(HttpStatusCode.OK);
        await SeedSwitchAsync(pluginId, IntegrationSettingKeys.EnabledValue);

        var remove = await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        remove.StatusCode.Should().Be(HttpStatusCode.OK);
        var removeBody = await remove.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        removeBody.GetProperty("requiresRestart").GetBoolean().Should().BeTrue();

        var list = await client.GetFromJsonAsync<List<System.Text.Json.JsonElement>>("/api/admin/plugins");
        list!.Should().NotContain(p => p.GetProperty("id").GetString() == pluginId);
        Directory.Exists(PluginsTestSeeds.InstalledFolder(pluginId)).Should().BeFalse();
        (await SwitchValueAsync(pluginId)).Should().Be(
            IntegrationSettingKeys.DisabledValue, "the still-loaded plugin must be refused from the commit on");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var removal = await db.ActivityLogs.AsNoTracking()
            .SingleAsync(l => l.Description.StartsWith("Removed Plugin") && l.Description.Contains(pluginId));
        removal.ClientId.Should().BeNull("a plugin removal concerns no client");
    }

    /// <summary>
    /// Removing an uploaded plugin keeps its stored entries and scope rows in every scope (plugin wide and per client),
    /// and every other plugin's rows, so a reinstall finds its state again.
    /// </summary>
    [Fact]
    public async Task Remove_KeepsThePluginsStoredDataAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        var otherPluginId = $"test-plugin-{Guid.NewGuid():N}";
        var install = await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(pluginId));
        install.StatusCode.Should().Be(HttpStatusCode.OK);
        await SeedStoreRowsAsync(pluginId, otherPluginId);

        var remove = await client.DeleteAsync($"/api/admin/plugins/{pluginId}");

        remove.StatusCode.Should().Be(HttpStatusCode.OK);
        (await StoredRowCountAsync(pluginId)).Should().Be(2, "a removal keeps the plugin's entries");
        (await ScopeRowCountAsync(pluginId)).Should().Be(2, "a removal keeps the plugin's scope rows");
        (await StoredRowCountAsync(otherPluginId)).Should().Be(2);
        (await ScopeRowCountAsync(otherPluginId)).Should().Be(2);
    }

    /// <summary>
    /// A valid id whose package is already gone (a folder removed by hand, or a retry after a removal whose save
    /// failed) is still switched off and recorded, keeps its data, and answers success without a restart.
    /// </summary>
    [Fact]
    public async Task PackageAlreadyGone_SwitchesOffWithoutARestartAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var pluginId = $"never-installed-{Guid.NewGuid():N}";
        await SeedSwitchAsync(pluginId, IntegrationSettingKeys.EnabledValue);
        await SeedStoreRowsAsync(pluginId);

        var response = await client.DeleteAsync($"/api/admin/plugins/{pluginId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("requiresRestart").GetBoolean().Should().BeFalse("no package was removed, nothing to unload");
        (await SwitchValueAsync(pluginId)).Should().Be(IntegrationSettingKeys.DisabledValue);
        (await StoredRowCountAsync(pluginId)).Should().Be(2, "the data is kept");
        (await ScopeRowCountAsync(pluginId)).Should().Be(2);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var line = await db.ActivityLogs.AsNoTracking().SingleAsync(l => l.Description.Contains(pluginId));
        line.Description.Should().NotStartWith("Removed Plugin", "the line says the package was already gone");
    }

    /// <summary>
    /// A valid id whose package is gone and that was never switched on here (no switch row) answers 404
    /// <c>PLUGIN_NOT_FOUND</c> and writes nothing: no settings row and no activity line.
    /// </summary>
    [Fact]
    public async Task PackageGoneAndNeverSwitchedOn_AnswersNotFoundAndWritesNothingAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var pluginId = $"never-installed-{Guid.NewGuid():N}";
        await SeedStoreRowsAsync(pluginId);

        var response = await client.DeleteAsync($"/api/admin/plugins/{pluginId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_NOT_FOUND);
        (await SwitchValueAsync(pluginId)).Should().BeNull("a mistyped id must not leave a settings row behind");
        (await ActivityLineCountAsync(pluginId)).Should().Be(0, "nothing was removed, so nothing is recorded");
        (await StoredRowCountAsync(pluginId)).Should().Be(2);
        (await ScopeRowCountAsync(pluginId)).Should().Be(2);
    }

    /// <summary>
    /// The switch and the audit line are written in one transaction: a removal whose transaction rolls back leaves the
    /// switch on and records nothing.
    /// </summary>
    [Fact]
    public async Task RolledBackRemoval_NeitherSwitchesOffNorRecordsAsync()
    {
        var rolledBackId = $"never-installed-{Guid.NewGuid():N}";
        await SeedSwitchAsync(rolledBackId, IntegrationSettingKeys.EnabledValue);
        await SeedStoreRowsAsync(rolledBackId);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await context.Database.BeginTransactionAsync();
            var result = await NewHandler(scope.ServiceProvider)
                .HandleAsync(new RemovePluginCommand(rolledBackId), CancellationToken.None);
            result.IsSuccess.Should().BeTrue();
            await transaction.RollbackAsync();
        }

        (await SwitchValueAsync(rolledBackId)).Should().Be(
            IntegrationSettingKeys.EnabledValue, "the switch rolled back with the transaction");
        (await ActivityLineCountAsync(rolledBackId)).Should().Be(0, "the audit line rolled back with it");
        (await StoredRowCountAsync(rolledBackId)).Should().Be(2);
    }

    /// <summary>A plugin shipped with the build answers 409 with <c>PLUGIN_BUILT_IN</c> and stays on disk.</summary>
    [Fact]
    public async Task ABuiltInPlugin_RefusesAndIsKeptAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var pluginId = $"test-builtin-{Guid.NewGuid():N}";
        var builtIn = PluginsTestSeeds.StageBuiltInPlugin(pluginId);

        try
        {
            var response = await client.DeleteAsync($"/api/admin/plugins/{pluginId}");

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_BUILT_IN);
            File.Exists(Path.Combine(builtIn, "plugin.json")).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(builtIn, recursive: true);
        }
    }

    /// <summary>A built-in plugin the loader refused for a broken manifest is still built-in: its removal answers 409 and the folder stays.</summary>
    [Fact]
    public async Task ARefusedBuiltInPlugin_CannotBeRemovedAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var pluginId = $"test-builtin-{Guid.NewGuid():N}";
        var builtIn = PluginsTestSeeds.StageBuiltInPlugin(pluginId);
        await File.WriteAllTextAsync(Path.Combine(builtIn, "plugin.json"), PluginsTestSeeds.ManifestJson(pluginId, ",\"manifestVersion\":9"));

        try
        {
            var response = await client.DeleteAsync($"/api/admin/plugins/{pluginId}");

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_BUILT_IN);
            File.Exists(Path.Combine(builtIn, "plugin.json")).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(builtIn, recursive: true);
        }
    }

    /// <summary>A removal refused because the plugin ships with the build leaves its switch and stored data untouched.</summary>
    [Fact]
    public async Task ABuiltInPlugin_KeepsItsSwitchAndStoredDataAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var pluginId = $"test-builtin-{Guid.NewGuid():N}";
        var builtIn = PluginsTestSeeds.StageBuiltInPlugin(pluginId);

        try
        {
            await SeedSwitchAsync(pluginId, IntegrationSettingKeys.EnabledValue);
            await SeedStoreRowsAsync(pluginId);

            var response = await client.DeleteAsync($"/api/admin/plugins/{pluginId}");

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await SwitchValueAsync(pluginId)).Should().Be(IntegrationSettingKeys.EnabledValue);
            (await StoredRowCountAsync(pluginId)).Should().Be(2);
            (await ScopeRowCountAsync(pluginId)).Should().Be(2);
        }
        finally
        {
            Directory.Delete(builtIn, recursive: true);
        }
    }

    /// <summary>An id breaking the plugin id rule is refused by the validator with <c>VALIDATION_ERROR</c>.</summary>
    [Fact]
    public async Task AnIdBreakingTheRule_RefusesOverHttpAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);

        var response = await client.DeleteAsync("/api/admin/plugins/Not_A.Valid-Id");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await PluginsTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>
    /// A plugin id that would escape the plugins folder is refused with <c>PLUGIN_ID_INVALID</c> by the handler
    /// itself, not only by the validator in front of it, and nothing is switched or staged. Calls the handler
    /// directly: a literal <c>..</c> path segment is normalised away by <see cref="Uri"/> before a real request
    /// could carry it.
    /// </summary>
    [Fact]
    public async Task APathTraversingId_RefusesAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var result = await NewHandler(scope.ServiceProvider)
            .HandleAsync(new RemovePluginCommand("../outside"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_ID_INVALID);
        context.ChangeTracker.HasChanges().Should().BeFalse("a refused removal stages no switch and no event");
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await PluginsTestSeeds.ClientRoleClientAsync(factory, "remove-plugin-forbidden");

        var response = await client.DeleteAsync($"/api/admin/plugins/never-installed-{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().DeleteAsync(
            $"/api/admin/plugins/never-installed-{Guid.NewGuid():N}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Builds the handler from a scope's own services, the way the bus would.</summary>
    /// <param name="services">The scope whose context, store and logger the handler uses.</param>
    /// <returns>The handler.</returns>
    private static RemovePluginHandler NewHandler(IServiceProvider services)
    {
        return new RemovePluginHandler(
            services.GetRequiredService<IPluginPackageStore>(),
            services.GetRequiredService<IActivityLogger>(),
            services.GetRequiredService<IHostpanelDbContext>());
    }

    /// <summary>Stores the plugin's switch row with the given value.</summary>
    /// <param name="pluginId">The plugin whose switch is stored.</param>
    /// <param name="value">The switch value, as the integration save writes it.</param>
    /// <returns>A task that completes when the row is saved.</returns>
    private async Task SeedSwitchAsync(string pluginId, string value)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Settings.Add(Setting.Create(IntegrationSettingKeys.EnabledKey(pluginId), value, null));
        await db.SaveChangesAsync();
    }

    /// <summary>Reads the plugin's stored switch value.</summary>
    /// <param name="pluginId">The plugin whose switch is read.</param>
    /// <returns>The stored value, or <see langword="null"/> when the plugin has no switch row.</returns>
    private async Task<string?> SwitchValueAsync(string pluginId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var key = IntegrationSettingKeys.EnabledKey(pluginId);
        return await db.Settings.AsNoTracking().Where(s => s.Key == key).Select(s => s.Value).SingleOrDefaultAsync();
    }

    /// <summary>
    /// Writes, for each given plugin, two store entries and their two scope rows: one plugin wide (no client
    /// reference) and one in a client scope. Each scope admits its entry, as the store does, so its totals cover the
    /// row the purge releases from it.
    /// </summary>
    /// <param name="pluginIds">The plugins to seed rows for.</param>
    /// <returns>A task that completes when the rows are saved.</returns>
    private async Task SeedStoreRowsAsync(params string[] pluginIds)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var pluginId in pluginIds)
        {
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

    /// <summary>Counts the activity log lines that name one plugin.</summary>
    /// <param name="pluginId">The plugin whose lines are counted.</param>
    /// <returns>How many activity log lines mention the plugin id.</returns>
    private async Task<int> ActivityLineCountAsync(string pluginId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ActivityLogs.AsNoTracking().CountAsync(l => l.Description.Contains(pluginId));
    }

    /// <summary>Counts the plugin store entries kept for one plugin across all scopes.</summary>
    /// <param name="pluginId">The plugin whose entries are counted.</param>
    /// <returns>The number of entries stored under that plugin id.</returns>
    private async Task<int> StoredRowCountAsync(string pluginId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PluginDataEntries.AsNoTracking().CountAsync(row => row.PluginId == pluginId);
    }

    /// <summary>Counts the plugin store scope rows kept for one plugin.</summary>
    /// <param name="pluginId">The plugin whose scope rows are counted.</param>
    /// <returns>The number of scope rows stored under that plugin id.</returns>
    private async Task<int> ScopeRowCountAsync(string pluginId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.PluginDataScopes.AsNoTracking().CountAsync(row => row.PluginId == pluginId);
    }
}
