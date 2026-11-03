using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Admin.Plugins.Commands.RemovePlugin;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
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
/// <c>DELETE /api/admin/plugins/{id}</c>: installing then removing a plugin takes it out of the list, switches it off,
/// writes the removal to the activity log and, through <c>PluginRemovedEvent</c> on the outbox, purges its stored
/// entries and scope rows in every scope and no other plugin's; a valid id whose package is already gone is still
/// switched off and purged and answers no restart when it has a switch row, and answers <c>PLUGIN_NOT_FOUND</c> and
/// writes nothing when it never had one; the switch and the event commit together, so a rolled-back removal
/// neither switches off nor purges; a plugin shipped with the build and an id breaking the plugin id rule are refused
/// and purge nothing; a caller with only the Client role is refused, and the route is closed to a caller with no
/// credential.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RemovePluginTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// How long a removal and the purge it sets off may take to settle. Generous because the first delivery on a host
    /// compiles the handler chains in memory, which takes seconds on a busy machine.
    /// </summary>
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(60);

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

        var remove = await DeleteAndSettleAsync(client, pluginId);
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
    /// Removing an uploaded plugin deletes its stored entries and scope rows in every scope (plugin wide and per
    /// client) once the removal's event is delivered, and leaves the rows of another plugin untouched.
    /// </summary>
    [Fact]
    public async Task Remove_PurgesThePluginsStoredDataInEveryScopeAndKeepsOthersAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        var otherPluginId = $"test-plugin-{Guid.NewGuid():N}";
        var install = await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(pluginId));
        install.StatusCode.Should().Be(HttpStatusCode.OK);
        await SeedStoreRowsAsync(pluginId, otherPluginId);

        var remove = await DeleteAndSettleAsync(client, pluginId);

        remove.StatusCode.Should().Be(HttpStatusCode.OK);
        (await StoredRowCountAsync(pluginId)).Should().Be(0, "every entry of the removed plugin is purged");
        (await ScopeRowCountAsync(pluginId)).Should().Be(0, "every scope row of the removed plugin is purged");
        (await StoredRowCountAsync(otherPluginId)).Should().Be(2, "another plugin's entries are not touched");
        (await ScopeRowCountAsync(otherPluginId)).Should().Be(2, "another plugin's scope rows are not touched");
    }

    /// <summary>
    /// A valid id whose package is already gone (a folder removed by hand, or a retry after a removal whose save
    /// failed) is still switched off, recorded and purged, and answers success without a restart.
    /// </summary>
    [Fact]
    public async Task PackageAlreadyGone_SwitchesOffAndPurgesWithoutARestartAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var pluginId = $"never-installed-{Guid.NewGuid():N}";
        await SeedSwitchAsync(pluginId, IntegrationSettingKeys.EnabledValue);
        await SeedStoreRowsAsync(pluginId);

        var response = await DeleteAndSettleAsync(client, pluginId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("requiresRestart").GetBoolean().Should().BeFalse("no package was removed, nothing to unload");
        (await SwitchValueAsync(pluginId)).Should().Be(IntegrationSettingKeys.DisabledValue);
        (await StoredRowCountAsync(pluginId)).Should().Be(0);
        (await ScopeRowCountAsync(pluginId)).Should().Be(0);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var line = await db.ActivityLogs.AsNoTracking().SingleAsync(l => l.Description.Contains(pluginId));
        line.Description.Should().NotStartWith("Removed Plugin", "the line says the package was already gone");
    }

    /// <summary>
    /// A valid id whose package is gone and that was never switched on here (no switch row) answers 404
    /// <c>PLUGIN_NOT_FOUND</c> and writes nothing: no settings row, no activity line and no removal event, so rows
    /// stored under that id are not purged.
    /// </summary>
    [Fact]
    public async Task PackageGoneAndNeverSwitchedOn_AnswersNotFoundAndWritesNothingAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var pluginId = $"never-installed-{Guid.NewGuid():N}";
        await SeedStoreRowsAsync(pluginId);

        var response = await DeleteAndSettleAsync(client, pluginId);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_NOT_FOUND);
        (await SwitchValueAsync(pluginId)).Should().BeNull("a mistyped id must not leave a settings row behind");
        (await ActivityLineCountAsync(pluginId)).Should().Be(0, "nothing was removed, so nothing is recorded");
        (await StoredRowCountAsync(pluginId)).Should().Be(2, "no removal event was raised, so nothing is purged");
        (await ScopeRowCountAsync(pluginId)).Should().Be(2);
    }

    /// <summary>
    /// The switch and the removal's event are written in one transaction: a removal whose transaction rolls back
    /// leaves the switch on and purges nothing, while a later committed removal proves the purge pipeline was running.
    /// </summary>
    [Fact]
    public async Task RolledBackRemoval_NeitherSwitchesOffNorPurgesAsync()
    {
        var rolledBackId = $"never-installed-{Guid.NewGuid():N}";
        var markerId = $"never-installed-{Guid.NewGuid():N}";
        await SeedSwitchAsync(rolledBackId, IntegrationSettingKeys.EnabledValue);
        await SeedSwitchAsync(markerId, IntegrationSettingKeys.EnabledValue);
        await SeedStoreRowsAsync(rolledBackId, markerId);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var transaction = await context.Database.BeginTransactionAsync();
            var result = await NewHandler(scope.ServiceProvider)
                .HandleAsync(new RemovePluginCommand(rolledBackId), CancellationToken.None);
            result.IsSuccess.Should().BeTrue();
            await transaction.RollbackAsync();
        }

        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        (await DeleteAndSettleAsync(client, markerId)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await StoredRowCountAsync(markerId)).Should().Be(0, "the committed removal was purged");
        (await SwitchValueAsync(rolledBackId)).Should().Be(
            IntegrationSettingKeys.EnabledValue, "the switch rolled back with the transaction");
        (await StoredRowCountAsync(rolledBackId)).Should().Be(2, "its event rolled back with the switch");
        (await ScopeRowCountAsync(rolledBackId)).Should().Be(2);
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

            var response = await DeleteAndSettleAsync(client, pluginId);

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

    /// <summary>
    /// A plugin installed under an id that was reserved later (<c>ui</c>) is still listed and can be removed, and its
    /// switch is turned off: the validator, the package store and the switch check only the id's shape on removal, so
    /// the operator is never left with a plugin folder the panel cannot reach. Staged on disk directly, because an
    /// install under that id is refused.
    /// </summary>
    [Fact]
    public async Task APluginUnderAReservedId_IsListedAndRemovableAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        const string reservedId = "ui";
        var folder = PluginsTestSeeds.InstalledFolder(reservedId);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "plugin.json"), PluginsTestSeeds.ManifestJson(reservedId));

        try
        {
            await SeedSwitchAsync(reservedId, IntegrationSettingKeys.EnabledValue);
            var listed = await client.GetFromJsonAsync<List<System.Text.Json.JsonElement>>("/api/admin/plugins");
            listed!.Should().Contain(p => p.GetProperty("id").GetString() == reservedId);

            var remove = await DeleteAndSettleAsync(client, reservedId);

            remove.StatusCode.Should().Be(HttpStatusCode.OK);
            Directory.Exists(folder).Should().BeFalse();
            (await SwitchValueAsync(reservedId)).Should().Be(IntegrationSettingKeys.DisabledValue);
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }

    /// <summary>
    /// An install whose manifest declares a reserved id is refused by the manifest rule with
    /// <c>PLUGIN_MANIFEST_ID_INVALID</c>, and nothing is left in the install folder.
    /// </summary>
    [Fact]
    public async Task InstallUnderAReservedId_RefusesAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);

        var response = await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip("ui"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_MANIFEST_ID_INVALID);
        Directory.Exists(PluginsTestSeeds.InstalledFolder("ui")).Should().BeFalse();
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
    /// <param name="services">The scope whose context, store, logger and clock the handler uses.</param>
    /// <returns>The handler.</returns>
    private static RemovePluginHandler NewHandler(IServiceProvider services)
    {
        return new RemovePluginHandler(
            services.GetRequiredService<IPluginPackageStore>(),
            services.GetRequiredService<IActivityLogger>(),
            services.GetRequiredService<IHostpanelDbContext>(),
            services.GetRequiredService<IClock>(),
            services.GetRequiredService<IPluginAvailabilityInvalidator>());
    }

    /// <summary>
    /// Sends the removal and waits until every message it set off, the purge included, has been handled. Recurring
    /// jobs are ignored, because their schedule runs beside every test and is not this test's outcome.
    /// </summary>
    /// <param name="client">The signed-in HTTP client.</param>
    /// <param name="pluginId">The plugin to remove.</param>
    /// <returns>The removal's response.</returns>
    private async Task<HttpResponseMessage> DeleteAndSettleAsync(HttpClient client, string pluginId)
    {
        HttpResponseMessage? response = null;
        await factory.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(SettleTimeout)
            .IgnoreMessagesMatchingType(type => typeof(IRecurringJob).IsAssignableFrom(type))
            .ExecuteAndWaitAsync(async Task (IMessageContext _) =>
            {
                response = await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
            });

        return response!;
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
