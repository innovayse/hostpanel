using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Identity;
using Hostpanel.Sdk.Plugins.Manifest;
using Hostpanel.Sdk.Plugins.Slots;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// The client area slot index (<c>GetClientPluginSlotQuery</c> through <c>ClientPluginSlotsController</c>), against
/// Postgres and test-only app plugins whose manifests put widgets into slots: the context is verified before anything is
/// listed and no plugin is ever built, widgets are filtered by switch, permission, product binding and the operator's
/// hidden list, labels follow the request's language, and the order is the same on every call.
/// </summary>
public sealed class GetClientPluginSlotTests : IClassFixture<AppPluginFixture>
{
    /// <summary>The host, the database and the probe that counts plugin constructions.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>Initializes the test, which starts from a probe that has seen nothing.</summary>
    /// <param name="fixture">The host, the database and the probe.</param>
    public GetClientPluginSlotTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
        _fixture.Probe.Reset();
    }

    /// <summary>The address of a client slot index.</summary>
    /// <param name="slot">The slot id.</param>
    /// <param name="serviceId">The service the page shows, or <see langword="null"/> for none.</param>
    /// <returns>The relative URL.</returns>
    private static string Url(string slot, int? serviceId = null)
    {
        return serviceId is { } id ? $"/api/client/plugins/slots/{slot}?serviceId={id}" : $"/api/client/plugins/slots/{slot}";
    }

    /// <summary>Reads the product of a seeded service.</summary>
    /// <param name="serviceId">The service id.</param>
    /// <returns>The id of the product the service orders.</returns>
    private async Task<int> ProductOfAsync(int serviceId)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await db.ClientServices.AsNoTracking().Where(s => s.Id == serviceId).Select(s => s.ProductId).SingleAsync();
    }

    /// <summary>
    /// A service of another client answers 404 <c>SERVICE_NOT_FOUND</c>, with the same body as a service that does not
    /// exist, and no plugin is built, so a service id cannot be probed.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AForeignServiceId_Answers404LikeAMissingOneAndNoPluginIsCalled()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "slot-owner");
        var stranger = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "slot-stranger");
        var foreign = await ServicesTestSeeds.SeedPendingServiceAsync(_fixture.Factory, stranger.ClientId);
        var pluginId = PluginActivationTestSeeds.NewId("probe");
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture, [pluginId], PluginSlotSeeds.Plugin(pluginId, PluginSlotSeeds.Widget("tab", PluginSlots.ClientServiceTabs)));
        using var http = AppPluginFixture.WithBearerOf(host, owner.Client);

        var foreignAnswer = await http.GetAsync(Url(PluginSlots.ClientServiceTabs, foreign));
        var missingAnswer = await http.GetAsync(Url(PluginSlots.ClientServiceTabs, int.MaxValue));

        foreignAnswer.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(foreignAnswer, HostpanelErrorCodes.SERVICE_NOT_FOUND);
        missingAnswer.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var foreignBody = await foreignAnswer.Content.ReadFromJsonAsync<JsonElement>();
        var missingBody = await missingAnswer.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var member in new[] { "type", "title", "status", "detail" })
        {
            missingBody.GetProperty(member).ToString().Should().Be(foreignBody.GetProperty(member).ToString());
        }

        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>The caller's own service is served, and still no plugin is built: the index is read from manifests.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnOwnService_ListsTheWidgetWithoutBuildingThePlugin()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "slot-own");
        var service = await ServicesTestSeeds.SeedPendingServiceAsync(_fixture.Factory, owner.ClientId);
        var pluginId = PluginActivationTestSeeds.NewId("probe");
        var widget = PluginSlotSeeds.Widget("tab", PluginSlots.ClientServiceTabs);
        using var host = await PluginSlotSeeds.HostAsync(_fixture, [pluginId], PluginSlotSeeds.Plugin(pluginId, widget));
        using var http = AppPluginFixture.WithBearerOf(host, owner.Client);

        var response = await http.GetAsync(Url(PluginSlots.ClientServiceTabs, service));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("slotId").GetString().Should().Be(PluginSlots.ClientServiceTabs);
        var listed = body.GetProperty("widgets").EnumerateArray().Single();
        listed.GetProperty("pluginId").GetString().Should().Be(pluginId);
        listed.GetProperty("id").GetString().Should().Be("tab");
        listed.GetProperty("view").GetString().Should().Be(widget.View);
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>A slot that needs a service and is asked without one answers 400 <c>PLUGIN_SLOT_CONTEXT_REQUIRED</c>.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ASlotThatNeedsAServiceWithNoneNamed_Answers400()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "slot-no-context");
        using var http = _fixture.ClientLike(owner.Client);

        var response = await http.GetAsync(Url(PluginSlots.ClientServiceTabs));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_SLOT_CONTEXT_REQUIRED);
    }

    /// <summary>An unknown slot and a slot of the admin panel both answer 404 <c>PLUGIN_SLOT_NOT_FOUND</c>.</summary>
    /// <param name="slot">The slot id asked for.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("client.nothing.here")]
    [InlineData(PluginSlots.AdminClientPanels)]
    public async Task AnUnknownOrAdminSlot_Answers404(string slot)
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "slot-unknown");
        using var http = _fixture.ClientLike(owner.Client);

        var response = await http.GetAsync(Url(slot));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_SLOT_NOT_FOUND);
    }

    /// <summary>With no plugin carrying a widget for the slot, the index is empty, not an error.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task WithNoPlugins_TheIndexIsEmpty()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "slot-empty");
        using var host = await PluginSlotSeeds.HostAsync(_fixture, []);
        using var http = AppPluginFixture.WithBearerOf(host, owner.Client);

        var response = await http.GetAsync(Url(PluginSlots.ClientDashboardCards));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await PluginSlotSeeds.WidgetsOfAsync(response)).Should().BeEmpty();
    }

    /// <summary>
    /// Widgets are ordered by <c>order</c>, then plugin id, then widget id, whatever order the plugins were loaded in, and
    /// the same call answers the same list twice.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TheOrder_IsOrderThenPluginIdThenWidgetId_AndStable()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "slot-order");
        var alpha = PluginActivationTestSeeds.NewId("alpha");
        var beta = PluginActivationTestSeeds.NewId("beta");
        var slot = PluginSlots.ClientDashboardCards;
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture,
            [alpha, beta],
            PluginSlotSeeds.Plugin(beta, PluginSlotSeeds.Widget("w", slot, order: 1), PluginSlotSeeds.Widget("first", slot, order: -5)),
            PluginSlotSeeds.Plugin(alpha, PluginSlotSeeds.Widget("z", slot, order: 1), PluginSlotSeeds.Widget("a", slot, order: 1)));
        using var http = AppPluginFixture.WithBearerOf(host, owner.Client);

        var first = await PluginSlotSeeds.WidgetsOfAsync(await http.GetAsync(Url(slot)));
        var second = await PluginSlotSeeds.WidgetsOfAsync(await http.GetAsync(Url(slot)));

        first.Should().Equal($"{beta}/first", $"{alpha}/a", $"{alpha}/z", $"{beta}/w");
        second.Should().Equal(first);
    }

    /// <summary>
    /// A widget whose plugin is switched off, or is not an app plugin, is not listed; the plugin that is switched on is.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task OnlyWidgetsOfEnabledAppPlugins_AreListed()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "slot-enabled");
        var on = PluginActivationTestSeeds.NewId("on");
        var off = PluginActivationTestSeeds.NewId("off");
        var payment = PluginActivationTestSeeds.NewId("pay");
        var slot = PluginSlots.ClientDashboardCards;
        var widget = PluginSlotSeeds.Widget("card", slot);
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture,
            [on, payment],
            PluginSlotSeeds.Plugin(on, widget),
            PluginSlotSeeds.Plugin(off, widget),
            PluginSlotSeeds.Plugin(payment, PluginType.Payment, new Dictionary<string, IReadOnlyDictionary<string, string>>(), widget));
        using var http = AppPluginFixture.WithBearerOf(host, owner.Client);

        var listed = await PluginSlotSeeds.WidgetsOfAsync(await http.GetAsync(Url(slot)));

        listed.Should().Equal($"{on}/card");
    }

    /// <summary>
    /// A widget is listed to a caller who holds its permission only: the account holder sees all three, a sub-user with
    /// another permission sees the one that needs none, and a widget that declares no permission is the holder's alone.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TheWidgetPermission_FiltersWhatACallerSees()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "slot-permission");
        var sub = await ClientsTestSeeds.LinkedToAsync(_fixture.Factory, "slot-permission-sub", owner.ClientId, ClientPermission.ViewDomains);
        var holder = await ClientsTestSeeds.LinkedToAsync(_fixture.Factory, "slot-permission-pay", owner.ClientId, ClientPermission.ViewPayInvoices);
        var pluginId = PluginActivationTestSeeds.NewId("perm");
        var slot = PluginSlots.ClientDashboardCards;
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture,
            [pluginId],
            PluginSlotSeeds.Plugin(
                pluginId,
                PluginSlotSeeds.Widget("1-any", slot, order: 1, permission: PluginPermissions.Any),
                PluginSlotSeeds.Widget("2-invoices", slot, order: 2, permission: nameof(ClientPermission.ViewPayInvoices)),
                PluginSlotSeeds.Widget("3-undeclared", slot, order: 3)));

        using var ownerHttp = AppPluginFixture.WithBearerOf(host, owner.Client);
        using var subHttp = AppPluginFixture.WithBearerOf(host, sub);
        using var holderHttp = AppPluginFixture.WithBearerOf(host, holder);

        (await PluginSlotSeeds.WidgetsOfAsync(await ownerHttp.GetAsync(Url(slot))))
            .Should().Equal($"{pluginId}/1-any", $"{pluginId}/2-invoices", $"{pluginId}/3-undeclared");
        (await PluginSlotSeeds.WidgetsOfAsync(await subHttp.GetAsync(Url(slot)))).Should().Equal($"{pluginId}/1-any");
        (await PluginSlotSeeds.WidgetsOfAsync(await holderHttp.GetAsync(Url(slot))))
            .Should().Equal($"{pluginId}/1-any", $"{pluginId}/2-invoices");
    }

    /// <summary>
    /// A widget bound to products is listed for a service of one of them, not for a service of another product, and a
    /// widget bound to none is listed for every service.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TheProductBinding_FiltersByTheProductOfTheService()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "slot-product");
        var service = await ServicesTestSeeds.SeedPendingServiceAsync(_fixture.Factory, owner.ClientId);
        var product = await ProductOfAsync(service);
        var pluginId = PluginActivationTestSeeds.NewId("bound");
        var slot = PluginSlots.ClientServiceTabs;
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture,
            [pluginId],
            PluginSlotSeeds.Plugin(
                pluginId,
                PluginSlotSeeds.Widget("everywhere", slot, order: 1),
                PluginSlotSeeds.Widget("this-product", slot, order: 2, productIds: [int.MaxValue, product]),
                PluginSlotSeeds.Widget("other-product", slot, order: 3, productIds: [int.MaxValue])));
        using var http = AppPluginFixture.WithBearerOf(host, owner.Client);

        var listed = await PluginSlotSeeds.WidgetsOfAsync(await http.GetAsync(Url(slot, service)));

        listed.Should().Equal($"{pluginId}/everywhere", $"{pluginId}/this-product");
    }

    /// <summary>A widget bound to a product is never listed in a slot that has no service to check the binding against.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ABoundWidgetInASlotWithoutAService_IsNotListed()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "slot-bound-dashboard");
        var pluginId = PluginActivationTestSeeds.NewId("bound");
        var slot = PluginSlots.ClientDashboardCards;
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture,
            [pluginId],
            PluginSlotSeeds.Plugin(pluginId, PluginSlotSeeds.Widget("bound", slot, productIds: [1]), PluginSlotSeeds.Widget("free", slot, order: 1)));
        using var http = AppPluginFixture.WithBearerOf(host, owner.Client);

        var listed = await PluginSlotSeeds.WidgetsOfAsync(await http.GetAsync(Url(slot)));

        listed.Should().Equal($"{pluginId}/free");
    }

    /// <summary>A widget the operator hid from the slot is absent, while the plugin's other widgets stay.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AHiddenWidget_IsAbsentAndItsSiblingStays()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "slot-hidden");
        var pluginId = PluginActivationTestSeeds.NewId("hide");
        var slot = PluginSlots.ClientDashboardCards;
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture,
            [pluginId],
            PluginSlotSeeds.Plugin(pluginId, PluginSlotSeeds.Widget("hidden", slot), PluginSlotSeeds.Widget("shown", slot, order: 1)));
        using var http = AppPluginFixture.WithBearerOf(host, owner.Client);
        await PluginSlotSeeds.HideAsync(_fixture.Factory, slot, pluginId, "hidden");

        var listed = await PluginSlotSeeds.WidgetsOfAsync(await http.GetAsync(Url(slot)));

        listed.Should().Equal($"{pluginId}/shown");
    }

    /// <summary>A title that is a manifest key is answered in the request's language, in English when that language lacks it, and plain text stays as written.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TheTitle_FollowsTheRequestLanguage()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "slot-language");
        var pluginId = PluginActivationTestSeeds.NewId("text");
        var slot = PluginSlots.ClientDashboardCards;
        var locales = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            ["en"] = new Dictionary<string, string> { ["card.usage"] = "Usage", ["card.english_only"] = "English only" },
            ["ru"] = new Dictionary<string, string> { ["card.usage"] = "Использование" },
        };
        using var host = await PluginSlotSeeds.HostAsync(
            _fixture,
            [pluginId],
            PluginSlotSeeds.Plugin(
                pluginId,
                PluginType.App,
                locales,
                PluginSlotSeeds.Widget("usage", slot, order: 1, title: "card.usage"),
                PluginSlotSeeds.Widget("english", slot, order: 2, title: "card.english_only"),
                PluginSlotSeeds.Widget("plain", slot, order: 3, title: "Plain title"),
                PluginSlotSeeds.Widget("untitled", slot, order: 4)));
        using var http = AppPluginFixture.WithBearerOf(host, owner.Client);
        http.DefaultRequestHeaders.Add("X-Language", "ru");

        var body = await (await http.GetAsync(Url(slot))).Content.ReadFromJsonAsync<JsonElement>();
        var titles = body.GetProperty("widgets").EnumerateArray()
            .Select(widget => widget.GetProperty("title").ValueKind == JsonValueKind.Null ? null : widget.GetProperty("title").GetString())
            .ToList();

        titles.Should().Equal("Использование", "English only", "Plain title", null);
    }

    /// <summary>When the identity plugin cannot say who the caller is, the answer is the retryable 503 with its code.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task WhenTheIdentityPluginCannotAnswer_TheAnswerIs503()
    {
        var registered = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "slot-identity-unavailable");
        using var derived = _fixture.Derive(services =>
            services.AddSingleton<IIdentityPluginResolver>(new ConflictingIdentityPluginResolver()));
        using var http = AppPluginFixture.WithBearerOf(derived, registered.Client);

        var response = await http.GetAsync(Url(PluginSlots.ClientDashboardCards));

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.IDENTITY_PLUGIN_UNAVAILABLE);
    }

    /// <summary>A member the identity plugin vouches for who has no client yet acts for no account: 404 <c>CLIENT_PROFILE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ACallerWithNoAccount_Answers404ClientProfileNotFound()
    {
        var (derived, http) = await NoAccountCallerAsync("slot-no-account");
        using (derived)
        using (http)
        {
            var response = await http.GetAsync(Url(PluginSlots.ClientDashboardCards));

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_PROFILE_NOT_FOUND);
        }
    }

    /// <summary>The slot is checked before the account: an unknown slot answers <c>PLUGIN_SLOT_NOT_FOUND</c> even for a caller with no account.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TheSlotIsCheckedBeforeTheAccount()
    {
        var (derived, http) = await NoAccountCallerAsync("slot-before-account");
        using (derived)
        using (http)
        {
            var response = await http.GetAsync(Url("client.nothing.here"));

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_SLOT_NOT_FOUND);
        }
    }

    /// <summary>Makes a caller the identity plugin vouches for whose external account has no client row.</summary>
    /// <param name="label">A label for the seeded user.</param>
    /// <returns>The derived host and the client that talks to it, both to dispose when the test ends.</returns>
    private async Task<(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> Host, HttpClient Http)> NoAccountCallerAsync(string label)
    {
        var user = await ClientsTestSeeds.CreateUnlinkedUserAsync(_fixture.Factory, label);
        var plugin = new StubClientResolver();
        plugin.Grant(
            user.UserId,
            new ClientAccess(new ExternalAccount(PluginMemberSeeds.Provider, $"acct-{Guid.NewGuid():N}", null, null), true, CanManagePaymentMethods: false));
        var derived = _fixture.Derive(services => services.AddSingleton<IIdentityPluginResolver>(new FakeIdentityPluginResolver(plugin)));

        return (derived, AppPluginFixture.WithBearerOf(derived, user.Client));
    }
}
