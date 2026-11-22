using System.Text.Json;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Host;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Slots;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Infrastructure.Plugins;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// Loaded plugins whose manifest declares capabilities, for the <c>endpoints</c> capability tests (#818, #1357): one with a
/// single admin route, and a serving plugin of any type with client, admin and callback routes, a menu, a page and the
/// service tab and panel widgets, so one set of assertions covers every plugin type.
/// </summary>
internal static class PluginCapabilitySeeds
{
    /// <summary>The admin route every seeded plugin declares, below its <c>ext</c> prefix.</summary>
    public const string AdminPath = "/stats";

    /// <summary>The client route a serving plugin declares, open to every client.</summary>
    public const string ClientPath = "/ping";

    /// <summary>The client route a serving plugin declares for the invoice permission, which a contact without it may not reach.</summary>
    public const string PermissionedPath = "/invoices";

    /// <summary>The client route on which a serving plugin reads the server connection of the service named in the path.</summary>
    public const string ConnectionPath = "/connection/{serviceId}";

    /// <summary>The callback route a serving plugin declares.</summary>
    public const string CallbackPath = "/hook";

    /// <summary>The id of the page, in both areas, a serving plugin declares.</summary>
    public const string PageId = "home";

    /// <summary>The path of the page, in both areas, a serving plugin declares.</summary>
    public const string PagePath = "/home";

    /// <summary>The id of the widget a serving plugin puts into <c>client.service.tabs</c>.</summary>
    public const string TabId = "tab";

    /// <summary>The id of the widget a serving plugin puts into <c>admin.service.panels</c>.</summary>
    public const string PanelId = "panel";

    /// <summary>The empty view document every page and widget of a serving plugin points at.</summary>
    private const string ViewJson = """{"schema":1,"blocks":[]}""";

    /// <summary>The class that answers a plugin which promises the endpoints capability and does not implement the contract.</summary>
    public static readonly Type NotImplementing = typeof(ConfigurationCapturingFulfilmentPlugin);

    /// <summary>The class a serving plugin of a type is answered by: it implements the type's contract and the endpoints contract.</summary>
    /// <param name="type">The plugin type.</param>
    /// <returns>The class.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> has no serving test class.</exception>
    public static Type ImplementationOf(PluginType type)
    {
        return type switch
        {
            PluginType.Payment => typeof(PaymentEndpointsPlugin),
            PluginType.Registrar => typeof(RegistrarEndpointsPlugin),
            PluginType.Provisioning => typeof(ProvisioningEndpointsPlugin),
            PluginType.Fulfilment => typeof(FulfilmentEndpointsPlugin),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "No serving test plugin of this type."),
        };
    }

    /// <summary>A loaded plugin of a type, with capabilities and one admin route, as if read from a validated manifest.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="type">The type the manifest declares.</param>
    /// <param name="implementation">The CLR type that answers.</param>
    /// <param name="capabilities">The capabilities the manifest lists.</param>
    /// <returns>The loaded plugin.</returns>
    public static LoadedPlugin Plugin(string id, PluginType type, Type implementation, params string[] capabilities)
    {
        var api = new PluginApi { Admin = [new PluginRoute { Method = PluginRouteMethod.Get, Path = AdminPath }] };
        var manifest = Manifest(id, type, implementation, capabilities, manifestVersion: 1, api, new PluginUi());

        return new LoadedPlugin(manifest, implementation, PluginViewDocuments.None);
    }

    /// <summary>
    /// A loaded plugin of a type that declares everything the host can serve: a client route, a route that reads the server
    /// connection of a service, an admin route, a callback, a menu and a page in both areas, and a widget in the client service
    /// tabs and the admin service panels slots. Whether the host serves any of it depends only on <paramref name="capabilities"/>
    /// and on <paramref name="implementation"/>.
    /// </summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="type">The type the manifest declares.</param>
    /// <param name="implementation">The CLR type that answers.</param>
    /// <param name="capabilities">The capabilities the manifest lists.</param>
    /// <returns>The loaded plugin, holding the view document each page and widget names.</returns>
    public static LoadedPlugin Serving(string id, PluginType type, Type implementation, params string[] capabilities)
    {
        var api = new PluginApi
        {
            Client =
            [
                new PluginRoute { Method = PluginRouteMethod.Get, Path = ClientPath, Permission = PluginPermissions.Any },
                new PluginRoute { Method = PluginRouteMethod.Get, Path = ConnectionPath, Permission = PluginPermissions.Any },
                new PluginRoute { Method = PluginRouteMethod.Get, Path = PermissionedPath, Permission = nameof(ClientPermission.ViewPayInvoices) },
            ],
            Admin = [new PluginRoute { Method = PluginRouteMethod.Get, Path = AdminPath }],
            Callbacks = [new PluginRoute { Method = PluginRouteMethod.Post, Path = CallbackPath }],
        };
        var ui = new PluginUi
        {
            Client = new PluginUiArea
            {
                Menus = [new PluginMenuItem { Id = "home-menu", Label = "Home", Page = PageId }],
                Pages = [Page(PluginPermissions.Any)],
            },
            Admin = new PluginUiArea
            {
                Menus = [new PluginMenuItem { Id = "home-menu", Label = "Home", Page = PageId }],
                Pages = [Page(null)],
            },
            Slots =
            [
                new PluginSlotEntry
                {
                    Id = TabId,
                    Slot = PluginSlots.ClientServiceTabs,
                    View = $"views/{TabId}.json",
                    Permission = PluginPermissions.Any,
                    ContextKeys = [PluginSlotContextKeys.ServiceId],
                },
                new PluginSlotEntry
                {
                    Id = PanelId,
                    Slot = PluginSlots.AdminServicePanels,
                    View = $"views/{PanelId}.json",
                    ContextKeys = [PluginSlotContextKeys.ServiceId],
                },
            ],
        };
        var manifest = Manifest(id, type, implementation, capabilities, manifestVersion: 2, api, ui);

        using var view = JsonDocument.Parse(ViewJson);
        var views = new Dictionary<string, JsonElement>
        {
            [$"views/{PageId}.json"] = view.RootElement.Clone(),
            [$"views/{TabId}.json"] = view.RootElement.Clone(),
            [$"views/{PanelId}.json"] = view.RootElement.Clone(),
        };

        return new LoadedPlugin(manifest, implementation, new PluginViewDocuments(views));
    }

    /// <summary>The page every serving plugin declares in each area.</summary>
    /// <param name="permission">The permission it needs; <see langword="null"/> in the admin area, which takes none.</param>
    /// <returns>The page.</returns>
    private static PluginPage Page(string? permission)
    {
        return new PluginPage
        {
            Id = PageId,
            Path = PagePath,
            Title = "Home",
            View = $"views/{PageId}.json",
            Permission = permission,
        };
    }

    /// <summary>The manifest of a seeded plugin, built on the one every plugin test starts from.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="type">The type the manifest declares.</param>
    /// <param name="implementation">The CLR type that answers.</param>
    /// <param name="capabilities">The capabilities the manifest lists.</param>
    /// <param name="manifestVersion">The manifest version; the ui block and the host access list need version 2.</param>
    /// <param name="api">The routes the manifest declares.</param>
    /// <param name="ui">The pages, menus and slots the manifest declares.</param>
    /// <returns>The manifest.</returns>
    private static PluginManifest Manifest(
        string id, PluginType type, Type implementation, string[] capabilities, int manifestVersion, PluginApi api, PluginUi ui)
    {
        var basis = PluginActivationTestSeeds.Plugin(id, type, implementation).Manifest;
        return new PluginManifest
        {
            ManifestVersion = manifestVersion,
            Id = basis.Id,
            Name = basis.Name,
            Version = basis.Version,
            Author = basis.Author,
            Description = basis.Description,
            Type = type,
            Category = basis.Category,
            EntryPoint = basis.EntryPoint,
            SdkVersion = basis.SdkVersion,
            Capabilities = capabilities,
            HostAccess = manifestVersion >= 2 ? new[] { PluginHostAccess.ServiceConnection } : Array.Empty<string>(),
            Api = api,
            Ui = ui,
        };
    }
}
