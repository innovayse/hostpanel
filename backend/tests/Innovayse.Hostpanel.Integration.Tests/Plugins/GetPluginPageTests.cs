using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Domain.Auth.Enums;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Plugins;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Hostpanel.Sdk.Packaging.Naming;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// One plugin page with its view (<c>GET api/client/plugins/ui/{pluginId}/pages/{pageId}</c> and the admin twin),
/// against Postgres and the test-only plugins of <see cref="PluginUiFixture"/>: the view is localised without touching
/// anything that is not a label, and a page that cannot be served answers one 404 whatever the reason, a sub-user who
/// lacks the page's permission included.
/// </summary>
public sealed class GetPluginPageTests : IClassFixture<PluginUiFixture>
{
    /// <summary>The position of the header block in the rich view.</summary>
    private const int HeaderBlock = 0;

    /// <summary>The position of the table block in the rich view.</summary>
    private const int TableBlock = 1;

    /// <summary>The position of the action block in the rich view.</summary>
    private const int ActionBlock = 2;

    /// <summary>A well-formed plugin id that no plugin uses.</summary>
    private const string UnknownPluginId = "no-such-plugin";

    /// <summary>Stands in a test case for the fixture's primary plugin id, which is only known at run time.</summary>
    private const string PrimaryPlaceholder = "{primary}";

    /// <summary>An id one character over <see cref="PluginIdentifier.MaxLength"/>, otherwise well formed.</summary>
    private const string OverLongId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    /// <summary>The members of an error body that must match for two refusals to be indistinguishable.</summary>
    private static readonly string[] ErrorMembers = ["type", "title", "status", "detail"];

    /// <summary>The host with the plugins.</summary>
    private readonly PluginUiFixture _fixture;

    /// <summary>Initializes the test.</summary>
    /// <param name="fixture">The host with the plugins.</param>
    public GetPluginPageTests(PluginUiFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>The address of a client area page.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="pageId">The page id.</param>
    /// <returns>The relative URL.</returns>
    private static string ClientUrl(string pluginId, string pageId) => $"/api/client/plugins/ui/{pluginId}/pages/{pageId}";

    /// <summary>The address of an admin area page.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="pageId">The page id.</param>
    /// <returns>The relative URL.</returns>
    private static string AdminUrl(string pluginId, string pageId) => $"/api/admin/plugins/ui/{pluginId}/pages/{pageId}";

    /// <summary>Reads a response body as JSON.</summary>
    /// <param name="response">The response.</param>
    /// <returns>The document's root, detached from the response.</returns>
    private static async Task<JsonElement> JsonOfAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    /// <summary>Gets one block of an answered page's view.</summary>
    /// <param name="page">The answered page.</param>
    /// <param name="position">The block's position in the view.</param>
    /// <returns>The block.</returns>
    private static JsonElement BlockOf(JsonElement page, int position)
    {
        return page.GetProperty("view").GetProperty("blocks")[position];
    }

    /// <summary>Asserts a response is the 404 <c>PLUGIN_PAGE_NOT_FOUND</c> with exactly the body of another.</summary>
    /// <param name="response">The response under test.</param>
    /// <param name="expected">The body of the reference refusal, an unknown page.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    private static async Task AssertSameNotFoundAsync(HttpResponseMessage response, JsonElement expected)
    {
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await JsonOfAsync(response);
        body.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.PLUGIN_PAGE_NOT_FOUND);
        foreach (var member in ErrorMembers)
        {
            body.GetProperty(member).ToString().Should().Be(expected.GetProperty(member).ToString());
        }
    }

    /// <summary>The home page answers 200 with the title and every label of the view in English, and nothing else changed.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_Owner_GetsTheLocalisedView_AndNothingButLabelsChange()
    {
        var (_, http) = await _fixture.OwnerAsync("page-owner-en");

        var response = await http.GetAsync(ClientUrl(_fixture.PluginId, PluginUiFixture.HomePage));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await JsonOfAsync(response);
        page.GetProperty("pluginId").GetString().Should().Be(_fixture.PluginId);
        page.GetProperty("id").GetString().Should().Be(PluginUiFixture.HomePage);
        page.GetProperty("path").GetString().Should().Be("/home");
        page.GetProperty("title").GetString().Should().Be("Home page");
        page.GetProperty("view").GetProperty("schema").GetInt32().Should().Be(1);

        var header = BlockOf(page, HeaderBlock);
        var table = BlockOf(page, TableBlock);
        var action = BlockOf(page, ActionBlock);
        header.GetProperty("title").GetString().Should().Be("Overview");
        header.GetProperty("id").GetString().Should().Be("head.title");
        var columns = table.GetProperty("columns").EnumerateArray().ToList();
        columns[0].GetProperty("label").GetString().Should().Be("Name");
        columns[0].GetProperty("key").GetString().Should().Be("col.name");
        columns[1].GetProperty("label").GetString().Should().Be("Plain text");
        action.GetProperty("label").GetString().Should().Be("Pay now");
        action.GetProperty("confirm").GetString().Should().Be("Are you sure?");
        action.GetProperty("area").GetString().Should().Be("client");
        var route = action.GetProperty("route");
        route.GetProperty("method").GetString().Should().Be("post");
        route.GetProperty("path").GetString().Should().Be(PluginUiFixture.PublishPath);
    }

    /// <summary>A page carries the origins its plugin's manifest declares in <c>externalOrigins</c>, as written there.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_Page_CarriesTheManifestExternalOrigins()
    {
        var (_, http) = await _fixture.OwnerAsync("page-origins");

        using var response = await http.GetAsync(ClientUrl(_fixture.PluginId, PluginUiFixture.HomePage));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var origins = (await JsonOfAsync(response)).GetProperty("externalOrigins").EnumerateArray()
            .Select(origin => origin.GetString()).ToList();
        origins.Should().Equal(PluginUiFixture.ExternalOrigin);
    }

    /// <summary>A plugin whose manifest declares no external origin answers an empty list, never a missing member or null.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_PageOfAPluginWithoutOrigins_CarriesAnEmptyList()
    {
        var (_, http) = await _fixture.OwnerAsync("page-no-origins");

        using var response = await http.GetAsync(ClientUrl(_fixture.SecondPluginId, PluginUiFixture.StartPage));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var origins = (await JsonOfAsync(response)).GetProperty("externalOrigins");
        origins.ValueKind.Should().Be(JsonValueKind.Array);
        origins.GetArrayLength().Should().Be(0);
    }

    /// <summary>The title and the view follow <c>X-Language</c>; a key the language lacks is answered in English.</summary>
    /// <param name="language">The <c>X-Language</c> to send.</param>
    /// <param name="title">The expected page title.</param>
    /// <param name="header">The expected header title.</param>
    /// <param name="column">The expected table column label, English for Armenian, whose texts lack it.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("en", "Home page", "Overview", "Name")]
    [InlineData("ru", "Главная страница", "Обзор", "Имя")]
    [InlineData("hy", "Գլխավոր էջ", "Ակնարկ", "Name")]
    public async Task Client_ViewFollowsTheRequestLanguage_WithTheEnglishFallback(string language, string title, string header, string column)
    {
        var (_, owner) = await _fixture.OwnerAsync($"page-lang-{language}");
        var http = PluginUiFixture.In(owner, language);

        var response = await http.GetAsync(ClientUrl(_fixture.PluginId, PluginUiFixture.HomePage));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await JsonOfAsync(response);
        page.GetProperty("title").GetString().Should().Be(title);
        BlockOf(page, HeaderBlock).GetProperty("title").GetString().Should().Be(header);
        BlockOf(page, TableBlock).GetProperty("columns").EnumerateArray().First().GetProperty("label").GetString().Should().Be(column);
    }

    /// <summary>A page whose title is plain text answers it as written, in every language.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_PlainTextTitle_PassesThrough()
    {
        var (_, owner) = await _fixture.OwnerAsync("page-plain");
        var http = PluginUiFixture.In(owner, "ru");

        var response = await http.GetAsync(ClientUrl(_fixture.PluginId, PluginUiFixture.AccountPage));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonOfAsync(response)).GetProperty("title").GetString().Should().Be("Account overview");
    }

    /// <summary>A sub-user holding the page's permission opens it.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_SubUserWithThePermission_OpensThePage()
    {
        var (owner, _) = await _fixture.OwnerAsync("page-sub-permitted");
        var sub = await _fixture.SubUserAsync("page-sub-permitted-sub", owner.ClientId, ClientPermission.ViewPayInvoices);

        var response = await sub.GetAsync(ClientUrl(_fixture.PluginId, PluginUiFixture.InvoicesPage));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A sub-user lacking the page's permission is answered 404 <c>PLUGIN_PAGE_NOT_FOUND</c>, not a permission refusal.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_SubUserWithoutThePermission_Gets404()
    {
        var (owner, _) = await _fixture.OwnerAsync("page-sub-denied");
        var sub = await _fixture.SubUserAsync("page-sub-denied-sub", owner.ClientId, ClientPermission.ViewDomains);

        var response = await sub.GetAsync(ClientUrl(_fixture.PluginId, PluginUiFixture.InvoicesPage));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_PAGE_NOT_FOUND);
    }

    /// <summary>
    /// A sub-user lacking the page's permission gets exactly the body an unknown page answers the same caller, so the
    /// refusal does not reveal that the page exists.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_SubUserWithoutThePermission_GetsTheUnknownPageBody()
    {
        var (owner, _) = await _fixture.OwnerAsync("page-sub-oracle");
        var sub = await _fixture.SubUserAsync("page-sub-oracle-sub", owner.ClientId, ClientPermission.ViewDomains);

        var unknown = await sub.GetAsync(ClientUrl(_fixture.PluginId, "no-such-page"));
        var denied = await sub.GetAsync(ClientUrl(_fixture.PluginId, PluginUiFixture.InvoicesPage));

        var expected = await JsonOfAsync(unknown);
        await AssertSameNotFoundAsync(unknown, expected);
        await AssertSameNotFoundAsync(denied, expected);
    }

    /// <summary>A page with no permission is the account holder's alone: a sub-user gets the unknown-page 404, the owner is served.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_PageWithoutPermission_IsForTheAccountHolderOnly()
    {
        var (owner, ownerHttp) = await _fixture.OwnerAsync("page-account-only");
        var sub = await _fixture.SubUserAsync("page-account-only-sub", owner.ClientId, ClientPermission.ViewPayInvoices);

        var unknown = await sub.GetAsync(ClientUrl(_fixture.PluginId, "no-such-page"));
        var asSub = await sub.GetAsync(ClientUrl(_fixture.PluginId, PluginUiFixture.AccountPage));
        var asOwner = await ownerHttp.GetAsync(ClientUrl(_fixture.PluginId, PluginUiFixture.AccountPage));

        await AssertSameNotFoundAsync(asSub, await JsonOfAsync(unknown));
        asOwner.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// An unknown plugin, a disabled plugin, a plugin of another type and a page the plugin did not declare answer with
    /// the same 404 body, so nothing about which of the four it is can be probed.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_UnknownDisabledNonAppAndUndeclared_AnswerTheSame404()
    {
        var (_, http) = await _fixture.OwnerAsync("page-same-404");

        var unknown = await http.GetAsync(ClientUrl("no-such-plugin", PluginUiFixture.HomePage));
        var disabled = await http.GetAsync(ClientUrl(_fixture.DisabledPluginId, PluginUiFixture.HomePage));
        var nonApp = await http.GetAsync(ClientUrl(_fixture.NotAppPluginId, PluginUiFixture.HomePage));
        var undeclared = await http.GetAsync(ClientUrl(_fixture.PluginId, "no-such-page"));

        var expected = await JsonOfAsync(unknown);
        foreach (var response in new[] { unknown, disabled, nonApp, undeclared })
        {
            await AssertSameNotFoundAsync(response, expected);
        }
    }

    /// <summary>A page of the other area is undeclared in this one: the admin page is not served to the client area.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_AnAdminPage_IsNotFound()
    {
        var (_, http) = await _fixture.OwnerAsync("page-admin-in-client");

        var response = await http.GetAsync(ClientUrl(_fixture.PluginId, PluginUiFixture.AdminPage));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_PAGE_NOT_FOUND);
    }

    /// <summary>A disabled plugin is 404 even to a sub-user who lacks the page's permission, so a refusal never reveals that it exists.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_DisabledPlugin_Answers404BeforeAnyPermissionCheck()
    {
        var (owner, _) = await _fixture.OwnerAsync("page-disabled-denied");
        var sub = await _fixture.SubUserAsync("page-disabled-denied-sub", owner.ClientId, ClientPermission.None);

        var response = await sub.GetAsync(ClientUrl(_fixture.DisabledPluginId, PluginUiFixture.HomePage));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_PAGE_NOT_FOUND);
    }

    /// <summary>
    /// A sub-user refused a declared page's permission gets exactly the body an unknown plugin answers the same caller, so
    /// the refusal tells neither that the plugin exists nor that it declares the page.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_RefusedPermission_AnswersTheUnknownPluginBody()
    {
        var (owner, _) = await _fixture.OwnerAsync("page-refused-vs-unknown");
        using var sub = await _fixture.SubUserAsync("page-refused-vs-unknown-sub", owner.ClientId, ClientPermission.ViewDomains);

        using var unknown = await sub.GetAsync(ClientUrl(UnknownPluginId, PluginUiFixture.InvoicesPage));
        using var refused = await sub.GetAsync(ClientUrl(_fixture.PluginId, PluginUiFixture.InvoicesPage));

        var expected = await JsonOfAsync(unknown);
        await AssertSameNotFoundAsync(unknown, expected);
        await AssertSameNotFoundAsync(refused, expected);
    }

    /// <summary>
    /// A caller who holds the client role but acts for no account reaches the handler and gets the unknown-plugin 404 for
    /// a page every client may open, never 403 or <c>CLIENT_PROFILE_NOT_FOUND</c>. The role is granted on purpose: without
    /// it (and with no identity plugin) the <c>Policies.Client</c> requirement itself refuses a roleless user with 403
    /// before any use case runs, which is the portal policy's answer, not this endpoint's.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_ClientRoleCallerOnNoAccount_GetsTheUnknownPluginBody()
    {
        var user = await ClientsTestSeeds.CreateUnlinkedUserAsync(_fixture.Factory, "page-no-account");
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISubjectRoleStore>()
                .AddAsync(user.UserId, Role.Client, CancellationToken.None);
        }

        using var http = _fixture.ClientLike(user.Client);

        using var unknown = await http.GetAsync(ClientUrl(UnknownPluginId, PluginUiFixture.HomePage));
        using var known = await http.GetAsync(ClientUrl(_fixture.PluginId, PluginUiFixture.HomePage));

        var expected = await JsonOfAsync(unknown);
        await AssertSameNotFoundAsync(unknown, expected);
        await AssertSameNotFoundAsync(known, expected);
    }

    /// <summary>
    /// When the identity plugin cannot say who the caller is, a known and an unknown plugin both answer the retryable 503:
    /// the caller is resolved before the plugin is looked up, so the outage reveals nothing about which plugins exist.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_IdentityPluginUnavailable_Answers503_ForAKnownAndAnUnknownPlugin()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "page-identity-outage");
        using var derived = _fixture.Host.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IIdentityPluginResolver>(new ConflictingIdentityPluginResolver())));
        using var http = AppPluginFixture.WithBearerOf(derived, owner.Client);

        using var known = await http.GetAsync(ClientUrl(_fixture.PluginId, PluginUiFixture.HomePage));
        using var unknown = await http.GetAsync(ClientUrl(UnknownPluginId, PluginUiFixture.HomePage));

        foreach (var response in new[] { known, unknown })
        {
            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.IDENTITY_PLUGIN_UNAVAILABLE);
        }
    }

    /// <summary>
    /// A plugin id that is not well formed or a page id of the wrong shape or length answers the unknown-plugin 404 body,
    /// not a 400, so a caller cannot learn which ids are well formed.
    /// </summary>
    /// <param name="pluginId">The plugin id to send; <c>{primary}</c> stands for the fixture's enabled plugin.</param>
    /// <param name="pageId">The page id to send.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("Upper-Case", PluginUiFixture.HomePage)]
    [InlineData("under_score", PluginUiFixture.HomePage)]
    [InlineData("double--hyphen", PluginUiFixture.HomePage)]
    [InlineData(OverLongId, PluginUiFixture.HomePage)]
    [InlineData(PrimaryPlaceholder, "Home")]
    [InlineData(PrimaryPlaceholder, "home.page")]
    [InlineData(PrimaryPlaceholder, "-home")]
    [InlineData(PrimaryPlaceholder, OverLongId)]
    public async Task Client_MalformedPluginOrPageId_AnswersTheUnknownPluginBody(string pluginId, string pageId)
    {
        var (_, http) = await _fixture.OwnerAsync($"page-malformed-{Guid.NewGuid():N}");
        var target = pluginId == PrimaryPlaceholder ? _fixture.PluginId : pluginId;

        using var unknown = await http.GetAsync(ClientUrl(UnknownPluginId, PluginUiFixture.HomePage));
        using var malformed = await http.GetAsync(ClientUrl(target, pageId));

        var expected = await JsonOfAsync(unknown);
        await AssertSameNotFoundAsync(malformed, expected);
    }

    /// <summary>The admin area answers a malformed plugin or page id with the same 404 an unknown plugin gets.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Admin_MalformedPluginOrPageId_AnswersTheUnknownPluginBody()
    {
        using var admin = await _fixture.AdminClientAsync();

        using var unknown = await admin.GetAsync(AdminUrl(UnknownPluginId, PluginUiFixture.AdminPage));
        using var badPlugin = await admin.GetAsync(AdminUrl(OverLongId, PluginUiFixture.AdminPage));
        using var badPage = await admin.GetAsync(AdminUrl(_fixture.PluginId, "Dashboard"));

        var expected = await JsonOfAsync(unknown);
        await AssertSameNotFoundAsync(badPlugin, expected);
        await AssertSameNotFoundAsync(badPage, expected);
    }

    /// <summary>
    /// A language the platform does not support (<c>X-Language: fr</c>) is answered in English, title and view alike, the
    /// same content an English request gets.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_UnsupportedLanguage_GetsEnglish()
    {
        var (_, owner) = await _fixture.OwnerAsync("page-lang-fr");
        var (_, english) = await _fixture.OwnerAsync("page-lang-fr-en");
        PluginUiFixture.In(owner, "fr");
        PluginUiFixture.In(english, "en");

        using var response = await owner.GetAsync(ClientUrl(_fixture.PluginId, PluginUiFixture.HomePage));
        using var reference = await english.GetAsync(ClientUrl(_fixture.PluginId, PluginUiFixture.HomePage));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await JsonOfAsync(response);
        page.GetProperty("title").GetString().Should().Be("Home page");
        BlockOf(page, HeaderBlock).GetProperty("title").GetString().Should().Be("Overview");
        page.GetProperty("view").GetRawText().Should().Be((await JsonOfAsync(reference)).GetProperty("view").GetRawText());
    }

    /// <summary>An administrator opens an admin page, localised, with the route object untouched.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Admin_GetsTheLocalisedAdminPage()
    {
        using var admin = await _fixture.AdminClientAsync();
        PluginUiFixture.In(admin, "ru");

        var response = await admin.GetAsync(AdminUrl(_fixture.PluginId, PluginUiFixture.AdminPage));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await JsonOfAsync(response);
        page.GetProperty("title").GetString().Should().Be("Страница администратора");
        var header = BlockOf(page, HeaderBlock);
        var action = BlockOf(page, ActionBlock);
        header.GetProperty("title").GetString().Should().Be("Обзор");
        action.GetProperty("label").GetString().Should().Be("Оплатить");
        action.GetProperty("area").GetString().Should().Be("admin");
        action.GetProperty("route").GetProperty("path").GetString().Should().Be(PluginUiFixture.PublishPath);
    }

    /// <summary>The admin area answers the same 404 for an unknown, a disabled and a non-app plugin, and a client page.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Admin_UnknownDisabledNonAppAndUndeclared_Answer404()
    {
        using var admin = await _fixture.AdminClientAsync();

        var responses = new[]
        {
            await admin.GetAsync(AdminUrl("no-such-plugin", PluginUiFixture.AdminPage)),
            await admin.GetAsync(AdminUrl(_fixture.DisabledPluginId, PluginUiFixture.AdminPage)),
            await admin.GetAsync(AdminUrl(_fixture.NotAppPluginId, PluginUiFixture.AdminPage)),
            await admin.GetAsync(AdminUrl(_fixture.PluginId, PluginUiFixture.HomePage)),
        };

        foreach (var response in responses)
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_PAGE_NOT_FOUND);
        }
    }

    /// <summary>A client token is refused at the admin page endpoint.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Admin_ClientToken_IsRefused()
    {
        var (_, http) = await _fixture.OwnerAsync("page-client-on-admin");

        var response = await http.GetAsync(AdminUrl(_fixture.PluginId, PluginUiFixture.AdminPage));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A page answers its layout (standard when none is declared) and the frame origins for the caller: the manifest's plus
    /// the caller's own active domains on products the operator bound to this plugin, never another client's; a sub-user who
    /// may not see services gets none of them.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_Page_AnswersLayoutAndOnlyTheCallersOwnBoundFrameOrigins()
    {
        var (owner, http) = await _fixture.OwnerAsync("frame-owner");
        var (stranger, _) = await _fixture.OwnerAsync("frame-stranger");
        var domain = $"site-{Guid.NewGuid():N}.example";
        var foreign = $"foreign-{Guid.NewGuid():N}.example";
        var productId = await SeedActiveServiceAsync(owner.ClientId, domain);
        var strangerProductId = await SeedActiveServiceAsync(stranger.ClientId, foreign);
        await BindAsync(_fixture.PluginId, productId);
        await BindAsync(_fixture.PluginId, strangerProductId);
        using var withoutServices = await _fixture.SubUserAsync("frame-sub", owner.ClientId, ClientPermission.ViewPayInvoices);

        var page = await JsonOfAsync(await http.GetAsync(ClientUrl(_fixture.PluginId, PluginUiFixture.HomePage)));
        var limited = await JsonOfAsync(await withoutServices.GetAsync(ClientUrl(_fixture.PluginId, PluginUiFixture.HomePage)));

        page.GetProperty("layout").GetString().Should().Be("standard");
        OriginsOf(page).Should().Equal(PluginUiFixture.ExternalOrigin, $"https://{domain}", $"https://www.{domain}");
        OriginsOf(limited).Should().Equal(PluginUiFixture.ExternalOrigin);
        LinkOriginsOf(page).Should().Equal(PluginUiFixture.ExternalOrigin, $"https://{domain}", $"https://www.{domain}");
        LinkOriginsOf(limited).Should().Equal(PluginUiFixture.ExternalOrigin);
    }

    /// <summary>
    /// A plugin published once for every installation links to the live site of the reader's own service without naming
    /// its domain: the link origins hold the manifest's origin and the https origin of the service bound to the plugin,
    /// and not the domain of a service on a product that is not bound, nor the panel's own origin.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_Page_LinkOrigins_HoldTheBoundServiceDomainsAndNotTheUnboundOnes()
    {
        var (owner, http) = await _fixture.OwnerAsync("link-owner");
        var bound = $"live-{Guid.NewGuid():N}.example";
        var unbound = $"unbound-{Guid.NewGuid():N}.example";
        var portal = _fixture.Factory.Services.GetRequiredService<IOptions<ClientPortalOptions>>().Value.BaseUrl;
        var boundProduct = await SeedActiveServiceAsync(owner.ClientId, bound);
        await SeedActiveServiceAsync(owner.ClientId, unbound);
        var ownProduct = await SeedActiveServiceAsync(owner.ClientId, new Uri(portal).Host);
        await BindAsync(_fixture.PluginId, boundProduct);
        await BindAsync(_fixture.PluginId, ownProduct);

        var page = await JsonOfAsync(await http.GetAsync(ClientUrl(_fixture.PluginId, PluginUiFixture.HomePage)));

        LinkOriginsOf(page).Should().Equal(PluginUiFixture.ExternalOrigin, $"https://{bound}", $"https://www.{bound}");
    }

    /// <summary>
    /// The caller's own active domain is not framed when its product is not bound, or is bound only to another plugin:
    /// the operator's binding is the one thing that lets a plugin reach a client's site.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_Page_OwnDomainOnAProductNotBoundToThePlugin_IsNotAFrameOrigin()
    {
        var (owner, http) = await _fixture.OwnerAsync("frame-unbound");
        await SeedActiveServiceAsync(owner.ClientId, $"unbound-{Guid.NewGuid():N}.example");
        var otherProductId = await SeedActiveServiceAsync(owner.ClientId, $"other-{Guid.NewGuid():N}.example");
        await BindAsync(_fixture.SecondPluginId, otherProductId);

        var page = await JsonOfAsync(await http.GetAsync(ClientUrl(_fixture.PluginId, PluginUiFixture.HomePage)));

        OriginsOf(page).Should().Equal(PluginUiFixture.ExternalOrigin);
    }

    /// <summary>
    /// A client typed the panel's own host as a service's domain: it stays off the list, though the page is the client's.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_Page_ThePanelsOwnOrigin_IsNeverAFrameOrigin()
    {
        var (owner, http) = await _fixture.OwnerAsync("frame-own");
        var portal = _fixture.Factory.Services.GetRequiredService<IOptions<ClientPortalOptions>>().Value.BaseUrl;
        var productId = await SeedActiveServiceAsync(owner.ClientId, new Uri(portal).Host);
        await BindAsync(_fixture.PluginId, productId);

        var page = await JsonOfAsync(await http.GetAsync(ClientUrl(_fixture.PluginId, PluginUiFixture.HomePage)));

        OriginsOf(page).Should().NotContain(new Uri(portal).GetLeftPart(UriPartial.Authority).ToLowerInvariant());
    }

    /// <summary>An admin page carries no frame origins: that panel renders no preview and acts for no client.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Admin_Page_HasNoFrameOrigins()
    {
        using var admin = await _fixture.AdminClientAsync();

        var page = await JsonOfAsync(await admin.GetAsync($"/api/admin/plugins/ui/{_fixture.PluginId}/pages/{PluginUiFixture.AdminPage}"));

        OriginsOf(page).Should().BeEmpty();
    }

    /// <summary>Reads the link origins (<c>externalOrigins</c>) of a page body.</summary>
    /// <param name="page">The page body.</param>
    /// <returns>The origins.</returns>
    private static List<string?> LinkOriginsOf(JsonElement page)
    {
        return page.GetProperty("externalOrigins").EnumerateArray().Select(origin => origin.GetString()).ToList();
    }

    /// <summary>Reads the frame origins of a page body.</summary>
    /// <param name="page">The page body.</param>
    /// <returns>The origins.</returns>
    private static List<string?> OriginsOf(JsonElement page)
    {
        return page.GetProperty("frameOrigins").EnumerateArray().Select(origin => origin.GetString()).ToList();
    }

    /// <summary>Seeds an active service on a domain, under a product of its own.</summary>
    /// <param name="clientId">The owner.</param>
    /// <param name="domain">The domain.</param>
    /// <returns>The product's id.</returns>
    private async Task<int> SeedActiveServiceAsync(int clientId, string domain)
    {
        var now = new DateTimeOffset(2032, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var scope = _fixture.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var group = ProductGroup.Create($"frame-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);
        var product = Product.Create(group.Id, $"Plan {Guid.NewGuid():N}", null, null, null, "plan", ProductType.SharedHosting, 0m, 0m, now);
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);
        var service = ClientService.CreateOrdered(clientId, product.Id, BillingCycle.Monthly, domain, 0m, 0m, null, null, now);
        service.Activate("ref", now, now);
        context.ClientServices.Add(service);
        await context.SaveChangesAsync(CancellationToken.None);
        return product.Id;
    }

    /// <summary>Stores an operator binding of a plugin's widget to a product.</summary>
    /// <param name="pluginId">The plugin.</param>
    /// <param name="productId">The product.</param>
    /// <returns>A task that completes when it is saved.</returns>
    private async Task BindAsync(string pluginId, int productId)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.PluginSlotProductBindings.Add(PluginSlotProductBinding.Create(pluginId, "client.dashboard.cards", "widget", productId));
        await context.SaveChangesAsync(CancellationToken.None);
    }
}
