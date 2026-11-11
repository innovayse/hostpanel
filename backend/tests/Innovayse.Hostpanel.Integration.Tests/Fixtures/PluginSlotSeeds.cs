using System.Net.Http.Json;
using System.Text.Json;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Identity;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// Builds the plugins, the hosts, the callers and the reads the plugin slot tests share (the slot index and the slot
/// widget view): app plugins whose manifests put widgets into slots, with view documents where a test reads them, a
/// host whose registry holds exactly them, a caller who acts for no account, and the widget list a slot answered.
/// </summary>
public static class PluginSlotSeeds
{
    /// <summary>A widget a plugin puts into a slot.</summary>
    /// <param name="id">The widget id, unique within the plugin.</param>
    /// <param name="slot">The slot id.</param>
    /// <param name="order">The position among the widgets of the slot.</param>
    /// <param name="title">The title: a key of the manifest locales or plain text; <see langword="null"/> for none.</param>
    /// <param name="permission">The permission a client slot widget needs; <see langword="null"/> for none declared.</param>
    /// <param name="productIds">The products the widget is bound to; none binds it to no product.</param>
    /// <returns>The widget.</returns>
    public static PluginSlotEntry Widget(
        string id, string slot, int order = 0, string? title = null, string? permission = null, params int[] productIds)
    {
        return new PluginSlotEntry
        {
            Id = id,
            Slot = slot,
            View = $"views/{id}.json",
            Title = title,
            Order = order,
            Permission = permission,
            ProductIds = productIds,
        };
    }

    /// <summary>Builds an app plugin whose manifest puts the given widgets into slots, with no view documents.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="widgets">The widgets the manifest declares.</param>
    /// <returns>The loaded plugin, answered by <see cref="FakeAppPlugin"/>.</returns>
    public static LoadedPlugin Plugin(string id, params PluginSlotEntry[] widgets)
    {
        return Plugin(id, PluginType.App, new Dictionary<string, IReadOnlyDictionary<string, string>>(), widgets);
    }

    /// <summary>
    /// Builds a plugin of a type whose manifest puts the given widgets into slots and carries locale texts, with no view
    /// documents (a widget whose view a test reads gets one through <see cref="WithView"/>).
    /// </summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="type">The type the manifest declares.</param>
    /// <param name="locales">The manifest's texts by locale, then by key.</param>
    /// <param name="widgets">The widgets the manifest declares.</param>
    /// <returns>The loaded plugin, answered by <see cref="FakeAppPlugin"/>.</returns>
    public static LoadedPlugin Plugin(
        string id,
        PluginType type,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> locales,
        params PluginSlotEntry[] widgets)
    {
        return Plugin(id, type, locales, [], widgets);
    }

    /// <summary>
    /// Builds a plugin of a type whose manifest puts the given widgets into slots, carries locale texts and declares
    /// <c>externalOrigins</c>, with no view documents.
    /// </summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="type">The type the manifest declares.</param>
    /// <param name="locales">The manifest's texts by locale, then by key.</param>
    /// <param name="externalOrigins">The origins the manifest declares for links and previews.</param>
    /// <param name="widgets">The widgets the manifest declares.</param>
    /// <returns>The loaded plugin, answered by <see cref="FakeAppPlugin"/>.</returns>
    public static LoadedPlugin Plugin(
        string id,
        PluginType type,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> locales,
        IReadOnlyList<string> externalOrigins,
        params PluginSlotEntry[] widgets)
    {
        var basis = PluginActivationTestSeeds.Plugin(id, type, typeof(FakeAppPlugin)).Manifest;
        var manifest = new PluginManifest
        {
            Id = basis.Id,
            Name = basis.Name,
            Version = basis.Version,
            Author = basis.Author,
            Description = basis.Description,
            Type = type,
            Category = basis.Category,
            EntryPoint = basis.EntryPoint,
            SdkVersion = basis.SdkVersion,
            Locales = locales,
            Ui = new PluginUi { Slots = widgets },
            ExternalOrigins = externalOrigins,
        };

        return new LoadedPlugin(manifest, typeof(FakeAppPlugin), PluginViewDocuments.None);
    }

    /// <summary>
    /// Adds a view document to a plugin, the way the loader keeps one it read and checked at load, so a test can read a
    /// widget's view without a package on disk. The plugin's other documents are kept.
    /// </summary>
    /// <param name="plugin">The plugin.</param>
    /// <param name="path">The package-relative path a widget names its view with, such as <c>views/tab.json</c>.</param>
    /// <param name="json">The view document.</param>
    /// <returns>A copy of the plugin holding the document.</returns>
    public static LoadedPlugin WithView(LoadedPlugin plugin, string path, string json)
    {
        var documents = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var known in plugin.Views.Paths)
        {
            // Every listed path has a document; Find answers null only for a path the plugin does not hold.
            documents[known] = plugin.Views.Find(known)!.Value;
        }

        using var parsed = JsonDocument.Parse(json);
        documents[path] = parsed.RootElement.Clone();
        return plugin with { Views = new PluginViewDocuments(documents) };
    }

    /// <summary>
    /// Derives a host whose registry holds exactly <paramref name="plugins"/>, and switches on the ones named.
    /// </summary>
    /// <param name="fixture">The app plugin host to derive from.</param>
    /// <param name="enabledIds">The ids of the plugins the operator has switched on.</param>
    /// <param name="plugins">The plugins the registry reports as loaded.</param>
    /// <returns>The host, to dispose when the test ends.</returns>
    public static async Task<WebApplicationFactory<Program>> HostAsync(
        AppPluginFixture fixture, IReadOnlyCollection<string> enabledIds, params LoadedPlugin[] plugins)
    {
        var registry = PluginActivationTestSeeds.Registry(plugins);

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await PluginActivationTestSeeds.EnableAsync(db, enabledIds.ToArray());
        }

        return fixture.Derive(services =>
        {
            // The host registers one instance under both names (see PluginLoader): the resolver reads the concrete
            // registry, the slot index builder the port, so both must be swapped or the builder lists the real plugins.
            services.RemoveAll<PluginRegistry>();
            services.RemoveAll<IPluginRegistry>();
            services.AddSingleton(registry);
            services.AddSingleton<IPluginRegistry>(registry);
        });
    }

    /// <summary>
    /// Makes a caller the identity plugin vouches for whose external account has no client row, so they act for no
    /// account. Shared by the slot index tests (<c>CLIENT_PROFILE_NOT_FOUND</c>) and the widget view tests (the one 404).
    /// </summary>
    /// <param name="fixture">The app plugin host to derive from.</param>
    /// <param name="label">A label for the seeded user, unique per test.</param>
    /// <returns>The derived host and the client signed in as the caller; dispose it when the test ends.</returns>
    public static async Task<NoAccountCaller> NoAccountCallerAsync(AppPluginFixture fixture, string label)
    {
        var user = await ClientsTestSeeds.CreateUnlinkedUserAsync(fixture.Factory, label);
        var plugin = new StubClientResolver();
        plugin.Grant(
            user.UserId,
            new ClientAccess(
                new ExternalAccount(PluginMemberSeeds.Provider, $"acct-{Guid.NewGuid():N}", null, null), true, CanManagePaymentMethods: false));
        var host = fixture.Derive(services => services.AddSingleton<IIdentityPluginResolver>(new FakeIdentityPluginResolver(plugin)));

        return new NoAccountCaller(host, AppPluginFixture.WithBearerOf(host, user.Client));
    }

    /// <summary>Hides a widget from a slot the way the operator does, through the hidden widgets setting.</summary>
    /// <param name="factory">The factory whose database holds the setting.</param>
    /// <param name="slot">The slot id.</param>
    /// <param name="pluginId">The plugin that declares the widget.</param>
    /// <param name="widgetId">The widget id.</param>
    /// <returns>A task that completes when the setting is saved.</returns>
    public static async Task HideAsync(IntegrationTestFactory factory, string slot, string pluginId, string widgetId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var entry = PluginSettingKeys.HiddenWidgetEntry(slot, pluginId, widgetId);

        var row = await db.Settings.FirstOrDefaultAsync(s => s.Key == PluginSettingKeys.HiddenSlotWidgets);
        if (row is null)
        {
            db.Settings.Add(Setting.Create(PluginSettingKeys.HiddenSlotWidgets, entry, null));
        }
        else
        {
            row.UpdateValue(row.Value.Length == 0 ? entry : $"{row.Value},{entry}");
        }

        await db.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Reads the widgets a slot index answered as <c>pluginId/widgetId</c>, in the order answered.</summary>
    /// <param name="response">The answered slot index.</param>
    /// <returns>One entry per widget.</returns>
    public static async Task<IReadOnlyList<string>> WidgetsOfAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("widgets").EnumerateArray()
            .Select(widget => $"{widget.GetProperty("pluginId").GetString()}/{widget.GetProperty("id").GetString()}")
            .ToList();
    }
}
