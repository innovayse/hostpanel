using System.Net.Http.Headers;
using System.Text.Json;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Fixtures;

/// <summary>
/// A host with test-only app plugins that declare pages, menus and views, built once for a test class. The registry
/// holds two enabled app plugins, registered <see cref="SecondPluginId"/> first although <see cref="PluginId"/> sorts
/// first, so an index that followed registration order instead of plugin id would fail; an app plugin that is
/// loaded but switched off, and an enabled plugin of the payment type; the last two declare pages that must never be
/// listed or served. The primary plugin ships English, Russian and Armenian texts, the Armenian ones missing one key,
/// and declares one external origin; the second declares none.
/// </summary>
public sealed class PluginUiFixture : IAsyncLifetime
{
    /// <summary>The page every client may open.</summary>
    public const string HomePage = "home";

    /// <summary>The page that needs <see cref="ClientPermission.ViewPayInvoices"/>.</summary>
    public const string InvoicesPage = "invoices";

    /// <summary>The page that declares no permission, which only the account holder may open.</summary>
    public const string AccountPage = "account";

    /// <summary>The one page of the admin area.</summary>
    public const string AdminPage = "dashboard";

    /// <summary>The one page of the second plugin.</summary>
    public const string StartPage = "start";

    /// <summary>The one web origin the primary plugin's manifest declares in <c>externalOrigins</c>.</summary>
    public const string ExternalOrigin = "https://docs.example.org";

    /// <summary>The route the views point their action at, declared by the primary plugin's client api.</summary>
    public const string PublishPath = "/services/{serviceId}/site/publish";

    /// <summary>Gets the shared API and database the host is derived from.</summary>
    public IntegrationTestFactory Factory { get; } = new();

    /// <summary>Gets the host whose registry holds the plugins below.</summary>
    public WebApplicationFactory<Program> Host { get; private set; } = null!;

    /// <summary>Gets the id of the primary enabled app plugin, which sorts before <see cref="SecondPluginId"/>.</summary>
    public string PluginId { get; } = PluginActivationTestSeeds.NewId("ui-a");

    /// <summary>Gets the id of the second enabled app plugin.</summary>
    public string SecondPluginId { get; } = PluginActivationTestSeeds.NewId("ui-b");

    /// <summary>Gets the id of an app plugin that is loaded but not switched on.</summary>
    public string DisabledPluginId { get; } = PluginActivationTestSeeds.NewId("ui-off");

    /// <summary>Gets the id of an enabled plugin of a type that does not serve pages, though its manifest declares some.</summary>
    public string NotAppPluginId { get; } = PluginActivationTestSeeds.NewId("ui-pay");

    /// <summary>Builds the factory, the database and the derived host with the plugins registered and switched on.</summary>
    /// <returns>A task that completes when the host is ready.</returns>
    public async Task InitializeAsync()
    {
        await Factory.InitializeAsync();

        // Registered against id order on purpose: the index must sort by plugin id, not keep the load order.
        var registry = PluginActivationTestSeeds.Registry(
            Second(),
            Primary(),
            Declaring(DisabledPluginId, PluginType.App),
            Declaring(NotAppPluginId, PluginType.Payment));

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await PluginActivationTestSeeds.EnableAsync(db, PluginId, SecondPluginId, NotAppPluginId);
        }

        Host = Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<PluginRegistry>();
                services.AddSingleton(registry);
            }));
    }

    /// <summary>Releases the host and the factory.</summary>
    /// <returns>A task that completes when both are released.</returns>
    public async Task DisposeAsync()
    {
        Host.Dispose();
        await Factory.DisposeAsync();
    }

    /// <summary>Makes a client of <see cref="Host"/> carrying the bearer token of an already signed-in client.</summary>
    /// <param name="signedIn">A client of another host whose bearer token is reused.</param>
    /// <returns>The client, to dispose when the test ends.</returns>
    public HttpClient ClientLike(HttpClient signedIn)
    {
        return AppPluginFixture.WithBearerOf(Host, signedIn);
    }

    /// <summary>Makes a client of <see cref="Host"/> carrying an administrator's bearer token.</summary>
    /// <returns>The client, to dispose when the test ends.</returns>
    public async Task<HttpClient> AdminClientAsync()
    {
        return await AdminClientOfAsync(Host, Factory);
    }

    /// <summary>Makes a client of a host carrying an administrator's bearer token.</summary>
    /// <param name="host">The host to talk to.</param>
    /// <param name="factory">The shared factory that signs the administrator in.</param>
    /// <returns>The client, to dispose when the test ends.</returns>
    public static async Task<HttpClient> AdminClientOfAsync(WebApplicationFactory<Program> host, IntegrationTestFactory factory)
    {
        var http = host.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }

    /// <summary>Registers an account holder and makes their client of <see cref="Host"/>.</summary>
    /// <param name="label">Distinguishes the registered address.</param>
    /// <returns>The account holder and a client of the host carrying their token.</returns>
    public async Task<PluginUiOwner> OwnerAsync(string label)
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(Factory, label);
        return new PluginUiOwner(owner, ClientLike(owner.Client));
    }

    /// <summary>Links a new user to an account with the given permissions and makes their client of <see cref="Host"/>.</summary>
    /// <param name="label">Distinguishes the created address.</param>
    /// <param name="clientId">The account to link to.</param>
    /// <param name="permissions">The permissions the link grants.</param>
    /// <returns>A client of the host carrying the sub-user's token.</returns>
    public async Task<HttpClient> SubUserAsync(string label, int clientId, ClientPermission permissions)
    {
        var linked = await ClientsTestSeeds.LinkedToAsync(Factory, label, clientId, permissions);
        return ClientLike(linked);
    }

    /// <summary>Asks the host for its answers in a language, by the <c>X-Language</c> header.</summary>
    /// <param name="http">The caller.</param>
    /// <param name="language">The language code: <c>en</c>, <c>ru</c> or <c>hy</c>.</param>
    /// <returns>The same caller, for chaining.</returns>
    public static HttpClient In(HttpClient http, string language)
    {
        http.DefaultRequestHeaders.Remove("X-Language");
        http.DefaultRequestHeaders.Add("X-Language", language);
        return http;
    }

    /// <summary>Parses a view document the way the loader hands it to the registry.</summary>
    /// <param name="json">The JSON text of the document.</param>
    /// <returns>The root element, detached from its parser.</returns>
    internal static JsonElement View(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>A declared client page.</summary>
    /// <param name="id">The page id.</param>
    /// <param name="title">The title: a text key or plain text.</param>
    /// <param name="permission">The permission, <see langword="null"/> for the account holder only.</param>
    /// <returns>The page; its path and view path derive from <paramref name="id"/>.</returns>
    private static PluginPage Page(string id, string title, string? permission)
    {
        return new PluginPage { Id = id, Path = $"/{id}", Title = title, View = $"views/{id}.json", Permission = permission };
    }

    /// <summary>A declared menu entry.</summary>
    /// <param name="id">The menu id.</param>
    /// <param name="label">The label: a text key or plain text.</param>
    /// <param name="page">The page the entry opens.</param>
    /// <param name="order">The sort order.</param>
    /// <returns>The menu entry.</returns>
    private static PluginMenuItem Menu(string id, string label, string page, int order)
    {
        return new PluginMenuItem { Id = id, Label = label, Page = page, Icon = "star", Order = order };
    }

    /// <summary>A route the views may name.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="path">The path template.</param>
    /// <param name="permission">The permission, <see langword="null"/> for none.</param>
    /// <returns>The route.</returns>
    private static PluginRoute Route(PluginRouteMethod method, string path, string? permission)
    {
        return new PluginRoute { Method = method, Path = path, Permission = permission };
    }

    /// <summary>A view with a header title, a table column label, an action with a label, a confirm text and a route.</summary>
    /// <param name="area">Which area the view is for, written into a non-label member to tell the views apart.</param>
    /// <returns>The view document.</returns>
    private static JsonElement RichView(string area)
    {
        return View($$"""
            {
              "schema": 1,
              "blocks": [
                { "kind": "header", "title": "head.title", "id": "head.title" },
                { "kind": "table", "columns": [ { "key": "col.name", "label": "col.name" }, { "key": "plain", "label": "Plain text" } ] },
                {
                  "kind": "action",
                  "area": "{{area}}",
                  "label": "button.pay",
                  "confirm": "button.confirm",
                  "route": { "method": "post", "path": "{{PublishPath}}" }
                }
              ]
            }
            """);
    }

    /// <summary>A view that is only a header.</summary>
    /// <returns>The view document.</returns>
    private static JsonElement SmallView()
    {
        return View("""{ "schema": 1, "blocks": [ { "kind": "header", "title": "head.title" } ] }""");
    }

    /// <summary>The primary plugin: three client pages, four menus, one admin page and menu, three languages.</summary>
    /// <returns>The loaded plugin.</returns>
    private LoadedPlugin Primary()
    {
        var english = new Dictionary<string, string>
        {
            ["menu.home"] = "Home",
            ["menu.invoices"] = "Invoices",
            ["menu.admin"] = "Administration",
            ["page.home"] = "Home page",
            ["page.invoices"] = "Invoices page",
            ["page.admin"] = "Admin page",
            ["head.title"] = "Overview",
            ["col.name"] = "Name",
            ["button.pay"] = "Pay now",
            ["button.confirm"] = "Are you sure?",
        };
        var russian = new Dictionary<string, string>
        {
            ["menu.home"] = "Главная",
            ["menu.invoices"] = "Счета",
            ["menu.admin"] = "Администрирование",
            ["page.home"] = "Главная страница",
            ["page.invoices"] = "Страница счетов",
            ["page.admin"] = "Страница администратора",
            ["head.title"] = "Обзор",
            ["col.name"] = "Имя",
            ["button.pay"] = "Оплатить",
            ["button.confirm"] = "Вы уверены?",
        };
        var armenian = new Dictionary<string, string>
        {
            ["menu.home"] = "Գլխավոր",
            ["menu.invoices"] = "Հաշիվներ",
            ["menu.admin"] = "Ադմինիստրացիա",
            ["page.home"] = "Գլխավոր էջ",
            ["page.invoices"] = "Հաշիվների էջ",
            ["page.admin"] = "Ադմինի էջ",
            ["head.title"] = "Ակնարկ",
            ["button.pay"] = "Վճարել",
            ["button.confirm"] = "Համոզվա՞ծ եք",
        };

        return Loaded(
            PluginId,
            PluginType.App,
            locales: new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["en"] = english,
                ["ru"] = russian,
                ["hy"] = armenian,
            },
            api: new PluginApi
            {
                Client =
                [
                    Route(PluginRouteMethod.Get, "/ping", PluginPermissions.Any),
                    Route(PluginRouteMethod.Post, PublishPath, nameof(ClientPermission.ViewPayInvoices)),
                ],
                Admin = [Route(PluginRouteMethod.Post, PublishPath, null)],
            },
            ui: new PluginUi
            {
                Client = new PluginUiArea
                {
                    Pages =
                    [
                        Page(HomePage, "page.home", PluginPermissions.Any),
                        Page(InvoicesPage, "page.invoices", nameof(ClientPermission.ViewPayInvoices)),
                        Page(AccountPage, "Account overview", null),
                    ],
                    Menus =
                    [
                        Menu("home", "menu.home", HomePage, 20),
                        Menu("invoices", "menu.invoices", InvoicesPage, 10),
                        Menu("account", "Account", AccountPage, 30),
                        Menu("invoices-shortcut", "menu.invoices", InvoicesPage, 40),
                    ],
                },
                Admin = new PluginUiArea
                {
                    Pages = [Page(AdminPage, "page.admin", null)],
                    Menus = [Menu("admin", "menu.admin", AdminPage, 10)],
                },
            },
            views: new Dictionary<string, JsonElement>
            {
                [$"views/{HomePage}.json"] = RichView("client"),
                [$"views/{InvoicesPage}.json"] = SmallView(),
                [$"views/{AccountPage}.json"] = SmallView(),
                [$"views/{AdminPage}.json"] = RichView("admin"),
            },
            externalOrigins: [ExternalOrigin]);
    }

    /// <summary>The second plugin: one client page and two menus, one of which ties on order with the primary plugin's home menu.</summary>
    /// <returns>The loaded plugin.</returns>
    private LoadedPlugin Second()
    {
        return Loaded(
            SecondPluginId,
            PluginType.App,
            locales: new Dictionary<string, IReadOnlyDictionary<string, string>>(),
            api: new PluginApi(),
            ui: new PluginUi
            {
                Client = new PluginUiArea
                {
                    Pages = [Page(StartPage, "Start here", PluginPermissions.Any)],
                    Menus = [Menu("second-first", "Second first", StartPage, 5), Menu("second-tie", "Second tie", StartPage, 20)],
                },
            },
            views: new Dictionary<string, JsonElement> { [$"views/{StartPage}.json"] = SmallView() });
    }

    /// <summary>A plugin that declares a client and an admin page and menu, which must never be listed or served.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="type">The plugin type the manifest declares.</param>
    /// <returns>The loaded plugin.</returns>
    private static LoadedPlugin Declaring(string id, PluginType type)
    {
        return Loaded(
            id,
            type,
            locales: new Dictionary<string, IReadOnlyDictionary<string, string>>(),
            api: new PluginApi(),
            ui: new PluginUi
            {
                Client = new PluginUiArea { Pages = [Page(HomePage, "Hidden", PluginPermissions.Any)], Menus = [Menu("hidden", "Hidden", HomePage, 1)] },
                Admin = new PluginUiArea { Pages = [Page(AdminPage, "Hidden", null)], Menus = [Menu("hidden-admin", "Hidden", AdminPage, 1)] },
            },
            views: new Dictionary<string, JsonElement>
            {
                [$"views/{HomePage}.json"] = SmallView(),
                [$"views/{AdminPage}.json"] = SmallView(),
            });
    }

    /// <summary>Builds a loaded plugin whose manifest carries the given texts, routes and ui.</summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="type">The plugin type the manifest declares.</param>
    /// <param name="locales">The texts per locale.</param>
    /// <param name="api">The routes the plugin answers.</param>
    /// <param name="ui">The pages and menus the plugin adds.</param>
    /// <param name="views">The view documents by the path the pages name.</param>
    /// <param name="externalOrigins">The manifest's <c>externalOrigins</c>; <see langword="null"/> leaves the member out.</param>
    /// <returns>The loaded plugin.</returns>
    private static LoadedPlugin Loaded(
        string id,
        PluginType type,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> locales,
        PluginApi api,
        PluginUi ui,
        IReadOnlyDictionary<string, JsonElement> views,
        IReadOnlyList<string>? externalOrigins = null)
    {
        var baseline = PluginActivationTestSeeds.Plugin(id, type, typeof(FakeAppPlugin)).Manifest;
        var manifest = new PluginManifest
        {
            ManifestVersion = 2,
            Id = baseline.Id,
            Name = baseline.Name,
            Version = baseline.Version,
            Author = baseline.Author,
            Description = baseline.Description,
            Type = type,
            Category = baseline.Category,
            EntryPoint = baseline.EntryPoint,
            SdkVersion = baseline.SdkVersion,
            Locales = locales,
            Api = api,
            Ui = ui,
            ExternalOrigins = externalOrigins!,
        };

        return new LoadedPlugin(manifest, typeof(FakeAppPlugin), new PluginViewDocuments(views));
    }
}
