using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Slots;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// One client slot widget's view (<c>GetClientPluginSlotWidgetQuery</c> through <c>ClientPluginSlotsController</c>), against
/// Postgres and test-only app plugins: served with its labels in the request's language only for a widget the index would
/// list for the same caller and context, never by building the plugin; the caller is resolved first, and every other
/// refusal is one 404 <c>PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND</c> with the same body and the same headers.
/// </summary>
public sealed class GetClientPluginSlotWidgetTests : IClassFixture<AppPluginFixture>
{
    /// <summary>The fixed moment the seeded rows are stamped with.</summary>
    private static readonly DateTimeOffset SeededAt = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A view document with one key-shaped text label, resolved through the manifest's locales.</summary>
    private const string ViewJson = """{"schema":1,"blocks":[{"kind":"text","text":"widget.hello"}]}""";

    /// <summary>The manifest texts the view's label resolves through, in the three languages.</summary>
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Locales =
        new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string> { ["widget.hello"] = "Hello" },
            ["ru"] = new Dictionary<string, string> { ["widget.hello"] = "Привет" },
            ["hy"] = new Dictionary<string, string> { ["widget.hello"] = "Բարեւ" },
        };

    /// <summary>The host, the database and the probe that counts plugin constructions.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>Initializes the test, which starts from a probe that has seen nothing.</summary>
    /// <param name="fixture">The host, the database and the probe.</param>
    public GetClientPluginSlotWidgetTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
        _fixture.Probe.Reset();
    }

    /// <summary>The address of a client slot widget's view.</summary>
    /// <param name="slot">The slot id.</param>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="widgetId">The widget id.</param>
    /// <param name="serviceId">The service the page shows, or <see langword="null"/> for none.</param>
    /// <returns>The relative URL.</returns>
    private static string Url(string slot, string pluginId, string widgetId, int? serviceId = null)
    {
        var path = $"/api/client/plugins/slots/{slot}/widgets/{pluginId}/{widgetId}";
        return serviceId is { } id ? $"{path}?serviceId={id}" : path;
    }

    /// <summary>A plugin with the given widgets and a view document for each, texts in en, ru and hy.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="widgets">The widgets.</param>
    /// <returns>The loaded plugin.</returns>
    private static Infrastructure.Plugins.LoadedPlugin WithViews(string pluginId, params PluginSlotEntry[] widgets)
    {
        var plugin = PluginSlotSeeds.Plugin(pluginId, global::Hostpanel.Sdk.Plugins.Enums.PluginType.App, Locales, widgets);
        foreach (var widget in widgets)
        {
            plugin = PluginSlotSeeds.WithView(plugin, widget.View, ViewJson);
        }

        return plugin;
    }

    /// <summary>Response headers that differ per request whatever the answer, left out of the comparison.</summary>
    private static readonly HashSet<string> PerRequestHeaders =
        new(StringComparer.OrdinalIgnoreCase) { "Date", "X-Request-Id", "traceparent", "tracestate" };

    /// <summary>Body members that differ per request whatever the answer, left out of the comparison.</summary>
    private static readonly HashSet<string> PerRequestMembers = new(StringComparer.Ordinal) { "instance", "traceId" };

    /// <summary>
    /// Asserts a 404 <c>PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND</c> and returns the whole answer for comparison: every header,
    /// response and content alike, and the raw body, less what differs per request.
    /// </summary>
    /// <param name="response">The answer.</param>
    /// <returns>The headers, sorted, and the body text with the per-request members removed.</returns>
    private static async Task<string> RefusalAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var raw = await response.Content.ReadAsStringAsync();
        var body = System.Text.Json.Nodes.JsonNode.Parse(raw)!.AsObject();
        body["type"]!.GetValue<string>().Should().Be(HostpanelErrorCodes.PLUGIN_SLOT_WIDGET_VIEW_NOT_FOUND);
        foreach (var member in PerRequestMembers)
        {
            body.Remove(member);
        }

        var headers = response.Headers.Concat(response.Content.Headers)
            .Where(header => !PerRequestHeaders.Contains(header.Key))
            .Select(header => $"{header.Key}: {string.Join(',', header.Value)}")
            .Order(StringComparer.OrdinalIgnoreCase);
        return string.Join('\n', headers) + "\n" + body.ToJsonString();
    }

    /// <summary>An own service's widget answers 200 with its view, labels in the request's language, and no plugin is built.</summary>
    /// <param name="language">The request's language.</param>
    /// <param name="expected">The resolved label.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("en", "Hello")]
    [InlineData("ru", "Привет")]
    [InlineData("hy", "Բարեւ")]
    public async Task AnOwnServicesWidget_AnswersItsViewInTheRequestLanguageWithoutBuildingThePlugin(string language, string expected)
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, $"widget-own-{language}");
        var service = await ServicesTestSeeds.SeedPendingServiceAsync(_fixture.Factory, owner.ClientId);
        var pluginId = PluginActivationTestSeeds.NewId("widget");
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture, [pluginId], WithViews(pluginId, PluginSlotSeeds.Widget("tab", PluginSlots.ClientServiceTabs)));
        using var http = AppPluginFixture.WithBearerOf(host, owner.Client);
        http.DefaultRequestHeaders.Add("X-Language", language);

        var response = await http.GetAsync(Url(PluginSlots.ClientServiceTabs, pluginId, "tab", service));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("pluginId").GetString().Should().Be(pluginId);
        body.GetProperty("id").GetString().Should().Be("tab");
        body.GetProperty("view").GetProperty("blocks")[0].GetProperty("text").GetString().Should().Be(expected);
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>
    /// Every refusal a signed-in client can provoke answers the same 404 body and headers: an unknown slot, an admin slot,
    /// a missing service, a foreign service (also for a sub-user who holds the widget's permission), a service slot with
    /// no service, a service id that is not a positive whole number (never a 400), an unknown widget, a widget id that
    /// only another plugin declares, a hidden one, one bound to another product, one of a switched-off plugin, one the
    /// caller lacks the permission for, and one whose view file is missing. No plugin is built for any of them.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task EveryRefusal_IsOne404WithTheSameBody()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "widget-refusals");
        var sub = await ClientsTestSeeds.LinkedToAsync(_fixture.Factory, "widget-refusals-sub", owner.ClientId, ClientPermission.ViewDomains);
        var permittedSub = await ClientsTestSeeds.LinkedToAsync(
            _fixture.Factory, "widget-refusals-sub-permitted", owner.ClientId, ClientPermission.ViewProductsServices);
        var stranger = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "widget-refusals-stranger");
        var own = await ServicesTestSeeds.SeedPendingServiceAsync(_fixture.Factory, owner.ClientId);
        var foreign = await ServicesTestSeeds.SeedPendingServiceAsync(_fixture.Factory, stranger.ClientId);
        var shown = PluginActivationTestSeeds.NewId("widget");
        var off = PluginActivationTestSeeds.NewId("widget-off");
        var noView = PluginActivationTestSeeds.NewId("widget-noview");
        var other = PluginActivationTestSeeds.NewId("widget-other");
        var cards = PluginSlots.ClientDashboardCards;
        var tabs = PluginSlots.ClientServiceTabs;
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture,
            [shown, noView, other],
            WithViews(
                shown,
                PluginSlotSeeds.Widget("card", cards),
                PluginSlotSeeds.Widget("hidden", cards),
                PluginSlotSeeds.Widget("bound", tabs, productIds: [int.MaxValue]),
                PluginSlotSeeds.Widget("tab", tabs)),
            WithViews(off, PluginSlotSeeds.Widget("card", cards)),
            PluginSlotSeeds.Plugin(noView, PluginSlotSeeds.Widget("card", cards)),
            WithViews(other, PluginSlotSeeds.Widget("only-other", cards)));
        await PluginSlotSeeds.HideAsync(_fixture.Factory, cards, shown, "hidden");
        using var http = AppPluginFixture.WithBearerOf(host, owner.Client);
        using var subHttp = AppPluginFixture.WithBearerOf(host, sub);
        using var permittedSubHttp = AppPluginFixture.WithBearerOf(host, permittedSub);

        var answers = new[]
        {
            await http.GetAsync(Url("client.nothing.here", shown, "card")),
            await http.GetAsync(Url(PluginSlots.AdminClientPanels, shown, "card")),
            await http.GetAsync(Url(tabs, shown, "tab", int.MaxValue)),
            await http.GetAsync(Url(tabs, shown, "tab", foreign)),
            await http.GetAsync(Url(tabs, shown, "tab")),
            await http.GetAsync(Url(cards, shown, "missing")),
            await http.GetAsync(Url(cards, shown, "hidden")),
            await http.GetAsync(Url(tabs, shown, "bound", own)),
            await http.GetAsync(Url(cards, off, "card")),
            await subHttp.GetAsync(Url(cards, shown, "card")),
            await http.GetAsync(Url(cards, noView, "card")),
            await http.GetAsync(Url(cards, shown, "only-other")),
            await permittedSubHttp.GetAsync(Url(tabs, shown, "tab", foreign)),
            await http.GetAsync($"{Url(tabs, shown, "tab")}?serviceId=abc"),
            await http.GetAsync($"{Url(tabs, shown, "tab")}?serviceId=-1"),
        };

        var bodies = new List<string>();
        foreach (var answer in answers)
        {
            bodies.Add(await RefusalAsync(answer));
        }

        bodies.Distinct().Should().ContainSingle();
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>A caller who acts for no account gets the very body an unknown widget gets.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ACallerWithNoAccount_GetsTheSameBodyAsAnUnknownWidget()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "widget-account-owner");
        using var noAccount = await PluginSlotSeeds.NoAccountCallerAsync(_fixture, "widget-no-account");
        using (var http = AppPluginFixture.WithBearerOf(noAccount.Host, owner.Client))
        {
            var refused = await RefusalAsync(await noAccount.Http.GetAsync(Url(PluginSlots.ClientDashboardCards, "acme", "card")));
            var unknown = await RefusalAsync(await http.GetAsync(Url(PluginSlots.ClientDashboardCards, "acme", "card")));

            refused.Should().Be(unknown);
        }
    }

    /// <summary>The refusal's sentence follows the request language, the same in every case.</summary>
    /// <param name="language">The request's language.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("ru")]
    [InlineData("hy")]
    public async Task TheRefusal_IsLocalised(string language)
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, $"widget-refusal-{language}");
        using var http = AppPluginFixture.WithBearerOf(_fixture.Factory, owner.Client);
        var english = await http.GetAsync(Url(PluginSlots.ClientDashboardCards, "acme", "card"));
        http.DefaultRequestHeaders.Add("X-Language", language);

        var localised = await http.GetAsync(Url(PluginSlots.ClientDashboardCards, "acme", "card"));

        var englishDetail = (await english.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString();
        var localisedDetail = (await localised.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString();
        await RefusalAsync(localised);
        localisedDetail.Should().NotBeNullOrWhiteSpace().And.NotBe(englishDetail);
    }

    /// <summary>
    /// The caller is resolved first: when the identity plugin cannot answer, the answer is the 503 whatever the ids name,
    /// an unknown slot included, so the outage cannot be told apart by the ids sent.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task WhenTheIdentityPluginCannotAnswer_TheAnswerIs503WhateverTheIds()
    {
        var registered = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "widget-identity-unavailable");
        using var derived = _fixture.Derive(services =>
            services.AddSingleton<IIdentityPluginResolver>(new ConflictingIdentityPluginResolver()));
        using var http = AppPluginFixture.WithBearerOf(derived, registered.Client);

        foreach (var url in new[] { Url(PluginSlots.ClientDashboardCards, "acme", "card"), Url("client.nothing.here", "acme", "card") })
        {
            var response = await http.GetAsync(url);

            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.IDENTITY_PLUGIN_UNAVAILABLE);
        }
    }

    /// <summary>
    /// The widget is served to a sub-user who holds its permission, to the holder when it is bound to the product of the
    /// caller's own service, and on a slot without a context a <c>serviceId</c> the page sends anyway is ignored.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task PermittedSubUser_OwnProductBinding_AndAnIgnoredServiceId_AreServed()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "widget-served");
        var sub = await ClientsTestSeeds.LinkedToAsync(
            _fixture.Factory, "widget-served-sub", owner.ClientId, ClientPermission.ViewProductsServices);
        var own = await ServicesTestSeeds.SeedPendingServiceAsync(_fixture.Factory, owner.ClientId);
        int product;
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.AppDbContext>();
            product = await db.ClientServices.Where(s => s.Id == own).Select(s => s.ProductId).SingleAsync();
        }

        var pluginId = PluginActivationTestSeeds.NewId("widget-served");
        var cards = PluginSlots.ClientDashboardCards;
        var tabs = PluginSlots.ClientServiceTabs;
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture,
            [pluginId],
            WithViews(
                pluginId,
                PluginSlotSeeds.Widget("perm", cards, permission: nameof(ClientPermission.ViewProductsServices)),
                PluginSlotSeeds.Widget("bound", tabs, productIds: [product])));
        using var http = AppPluginFixture.WithBearerOf(host, owner.Client);
        using var subHttp = AppPluginFixture.WithBearerOf(host, sub);

        var bySub = await subHttp.GetAsync(Url(cards, pluginId, "perm"));
        var bound = await http.GetAsync(Url(tabs, pluginId, "bound", own));
        var ignored = await http.GetAsync(Url(cards, pluginId, "perm", own));

        bySub.StatusCode.Should().Be(HttpStatusCode.OK);
        bound.StatusCode.Should().Be(HttpStatusCode.OK);
        ignored.StatusCode.Should().Be(HttpStatusCode.OK);
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>
    /// A widget's link origins hold the manifest's origins and the https host of the caller's own active service on a
    /// product the operator bound to the plugin, so a plugin published once links to the live site without naming its
    /// domain; a plugin with no binding gets the manifest's origins alone.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TheWidgetsLinkOrigins_HoldTheBoundServiceDomain_AndOnlyThen()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "widget-links");
        var domain = $"live-{Guid.NewGuid():N}.example";
        var service = await ServicesTestSeeds.SeedPendingServiceAsync(_fixture.Factory, owner.ClientId, domain);
        int product;
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.AppDbContext>();
            var row = await db.ClientServices.SingleAsync(s => s.Id == service);
            var now = SeededAt;
            row.Activate("ref", now, now);
            product = row.ProductId;
            await db.SaveChangesAsync(CancellationToken.None);
        }

        var pluginId = PluginActivationTestSeeds.NewId("widget-links");
        var tabs = PluginSlots.ClientServiceTabs;
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture, [pluginId], WithViews(pluginId, PluginSlotSeeds.Widget("tab", tabs)));
        using var http = AppPluginFixture.WithBearerOf(host, owner.Client);

        var before = await http.GetFromJsonAsync<JsonElement>(Url(tabs, pluginId, "tab", service));
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.AppDbContext>();
            db.PluginSlotProductBindings.Add(Domain.Plugins.PluginSlotProductBinding.Create(pluginId, tabs, "tab", product));
            await db.SaveChangesAsync(CancellationToken.None);
        }

        var after = await http.GetFromJsonAsync<JsonElement>(Url(tabs, pluginId, "tab", service));

        before.GetProperty("externalOrigins").GetArrayLength().Should().Be(0);
        after.GetProperty("externalOrigins").EnumerateArray().Select(o => o.GetString())
            .Should().Equal($"https://{domain}", $"https://www.{domain}");
    }

    /// <summary>
    /// A sub-user who may not see services gets only the manifest's link origins on a widget, though the account holder
    /// gets the bound service domain as well: the domains follow the visibility rule of the host API.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TheWidgetsLinkOrigins_ForASubUserWithoutViewProductsServices_AreTheManifestsOnly()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "widget-links-owner");
        var sub = await ClientsTestSeeds.LinkedToAsync(
            _fixture.Factory, "widget-links-sub", owner.ClientId, ClientPermission.ViewDomains);
        var domain = $"live-{Guid.NewGuid():N}.example";
        var service = await ServicesTestSeeds.SeedPendingServiceAsync(_fixture.Factory, owner.ClientId, domain);
        var pluginId = PluginActivationTestSeeds.NewId("widget-links-sub");
        var cards = PluginSlots.ClientDashboardCards;
        var tabs = PluginSlots.ClientServiceTabs;
        int product;
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Infrastructure.Persistence.AppDbContext>();
            var row = await db.ClientServices.SingleAsync(s => s.Id == service);
            var now = SeededAt;
            row.Activate("ref", now, now);
            product = row.ProductId;
            db.PluginSlotProductBindings.Add(Domain.Plugins.PluginSlotProductBinding.Create(pluginId, tabs, "tab", product));
            await db.SaveChangesAsync(CancellationToken.None);
        }

        var plugin = PluginSlotSeeds.Plugin(
            pluginId,
            global::Hostpanel.Sdk.Plugins.Enums.PluginType.App,
            Locales,
            ["https://docs.example.org"],
            PluginSlotSeeds.Widget("card", cards, permission: nameof(ClientPermission.ViewDomains)));
        using var host = await PluginSlotSeeds.HostAsync(_fixture, [pluginId], PluginSlotSeeds.WithView(plugin, "views/card.json", ViewJson));
        using var ownerHttp = AppPluginFixture.WithBearerOf(host, owner.Client);
        using var subHttp = AppPluginFixture.WithBearerOf(host, sub);

        var holder = await ownerHttp.GetFromJsonAsync<JsonElement>(Url(cards, pluginId, "card"));
        var limited = await subHttp.GetFromJsonAsync<JsonElement>(Url(cards, pluginId, "card"));

        holder.GetProperty("externalOrigins").EnumerateArray().Select(o => o.GetString())
            .Should().Equal("https://docs.example.org", $"https://{domain}", $"https://www.{domain}");
        limited.GetProperty("externalOrigins").EnumerateArray().Select(o => o.GetString())
            .Should().Equal("https://docs.example.org");
    }
}
