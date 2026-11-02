using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.SDK.Plugins.Endpoints;
using Innovayse.SDK.Plugins.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// The client area door to an app plugin (<c>CallClientPluginCommand</c> through <c>ClientPluginCallsController</c>),
/// against Postgres and the test-only plugins of <see cref="AppPluginFixture"/>: every refusal happens before plugin
/// code runs, a sub-user and an identity plugin member are held to the route's permission, the plugin only sees opaque
/// references, and what the plugin answers or throws becomes the host's standard reply.
/// </summary>
public sealed partial class CallClientPluginTests : IClassFixture<AppPluginFixture>
{
    /// <summary>The words a stack trace or an exception type would put in a response.</summary>
    private static readonly string[] LeakMarkers = ["secret-detail", "InvalidOperationException", "   at ", "Innovayse.Hostpanel"];

    /// <summary>The host, the database and the plugins.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>Initializes the test, which starts from a probe that has seen nothing and echoes.</summary>
    /// <param name="fixture">The host, the database and the plugins.</param>
    public CallClientPluginTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
        _fixture.Probe.Reset();
    }

    /// <summary>The shape of an opaque reference: 43 URL-safe characters.</summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"\A[A-Za-z0-9_-]{43}\z")]
    private static partial Regex OpaqueReference();

    /// <summary>The address of a client area plugin route.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="path">The path below the plugin's prefix.</param>
    /// <returns>The relative URL.</returns>
    private static string Url(string pluginId, string path) => $"/api/client/plugins/{pluginId}/{path}";

    /// <summary>Sends a request to a client area plugin route.</summary>
    /// <param name="http">The caller.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="path">The path below the plugin's prefix.</param>
    /// <param name="content">The body, or <see langword="null"/>.</param>
    /// <returns>The response.</returns>
    private static async Task<HttpResponseMessage> CallAsync(
        HttpClient http, HttpMethod method, string pluginId, string path, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, Url(pluginId, path)) { Content = content };
        return await http.SendAsync(request);
    }

    /// <summary>Reads a response body as JSON.</summary>
    /// <param name="response">The response.</param>
    /// <returns>The document's root, detached from the response.</returns>
    private static async Task<JsonElement> JsonOfAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    /// <summary>Registers an account holder and makes their client of the plugin host.</summary>
    /// <param name="label">Distinguishes the registered address.</param>
    /// <returns>The account holder and a client of the plugin host carrying their token.</returns>
    private async Task<(SignedInClient Owner, HttpClient Http)> OwnerAsync(string label)
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, label);
        return (owner, _fixture.ClientLike(owner.Client));
    }

    /// <summary>Links a new user to an account with the given permissions and makes their client of the plugin host.</summary>
    /// <param name="label">Distinguishes the created address.</param>
    /// <param name="clientId">The account to link to.</param>
    /// <param name="permissions">The permissions the link grants.</param>
    /// <returns>A client of the plugin host carrying the sub-user's token.</returns>
    private async Task<HttpClient> SubUserAsync(string label, int clientId, ClientPermission permissions)
    {
        var linked = await ClientsTestSeeds.LinkedToAsync(_fixture.Factory, label, clientId, permissions);
        return _fixture.ClientLike(linked);
    }

    /// <summary>A route nobody declared answers 404 and no plugin code runs, not even a constructor.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnUndeclaredRoute_Answers404WithoutRunningThePlugin()
    {
        var (_, http) = await OwnerAsync("undeclared-route");

        var response = await CallAsync(http, HttpMethod.Get, _fixture.PluginId, "nothing/here");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_ROUTE_NOT_FOUND);
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>A declared path with a method the plugin did not declare for it is as absent as an undeclared path.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ADeclaredPathWithAnotherMethod_Answers404()
    {
        var (_, http) = await OwnerAsync("undeclared-method");

        var response = await CallAsync(http, HttpMethod.Delete, _fixture.PluginId, "ping");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_ROUTE_NOT_FOUND);
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>
    /// An unknown plugin, a plugin that is switched off, a plugin of a type that serves no routes and a route that is
    /// not declared answer with the same body, so nothing about which of the four it is can be probed.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UnknownDisabledNonAppAndUndeclared_AnswerTheSame404()
    {
        var (_, http) = await OwnerAsync("same-404");

        var unknown = await CallAsync(http, HttpMethod.Get, "no-such-plugin", "ping");
        var disabled = await CallAsync(http, HttpMethod.Get, _fixture.DisabledPluginId, "ping");
        var nonApp = await CallAsync(http, HttpMethod.Get, _fixture.NotAppPluginId, "ping");
        var undeclared = await CallAsync(http, HttpMethod.Get, _fixture.PluginId, "nothing");

        var expected = await JsonOfAsync(unknown);
        foreach (var response in new[] { unknown, disabled, nonApp, undeclared })
        {
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            var body = await JsonOfAsync(response);
            foreach (var member in new[] { "type", "title", "status", "detail" })
            {
                body.GetProperty(member).ToString().Should().Be(expected.GetProperty(member).ToString());
            }
        }

        expected.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.PLUGIN_ROUTE_NOT_FOUND);
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>A disabled plugin is 404 even to a caller who lacks the route's permission, so a refusal never reveals that it exists.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ADisabledPlugin_Answers404EvenToACallerWithoutThePermission()
    {
        var (owner, _) = await OwnerAsync("disabled-no-permission");
        var sub = await SubUserAsync("disabled-no-permission-sub", owner.ClientId, ClientPermission.ViewDomains);

        var response = await CallAsync(sub, HttpMethod.Get, _fixture.DisabledPluginId, "invoices/1");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_ROUTE_NOT_FOUND);
    }

    /// <summary>A sub-user without the route's permission is refused with the existing client permission code, before plugin code runs.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AMissingPermission_Answers403BeforeAnyPluginCodeRuns()
    {
        var (owner, _) = await OwnerAsync("missing-permission");
        var sub = await SubUserAsync("missing-permission-sub", owner.ClientId, ClientPermission.ViewDomains);

        var response = await CallAsync(sub, HttpMethod.Get, _fixture.PluginId, "invoices/1");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>
    /// A sub-user who holds the permission is served, and the plugin sees the route that matched, the route values and
    /// the permissions the sub-user holds there, as names.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ASubUserWithThePermission_IsServedAndThePluginSeesWhatTheyHold()
    {
        var (owner, _) = await OwnerAsync("sub-user-permitted");
        var sub = await SubUserAsync("sub-user-permitted-sub", owner.ClientId, ClientPermission.ViewPayInvoices | ClientPermission.ViewDomains);

        var response = await CallAsync(sub, HttpMethod.Get, _fixture.PluginId, "invoices/42");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var seen = await JsonOfAsync(response);
        seen.GetProperty("area").GetString().Should().Be("Client");
        seen.GetProperty("method").GetString().Should().Be("Get");
        seen.GetProperty("route").GetString().Should().Be("/invoices/{id}");
        seen.GetProperty("routeValues").GetProperty("id").GetString().Should().Be("42");
        seen.GetProperty("permissions").EnumerateArray().Select(p => p.GetString())
            .Should().BeEquivalentTo(nameof(ClientPermission.ViewPayInvoices), nameof(ClientPermission.ViewDomains));
    }

    /// <summary>The account holder holds every permission, so the plugin is told every single flag and neither <c>None</c> nor <c>All</c>.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TheAccountHolder_IsTold_EverySingleFlag()
    {
        var (_, http) = await OwnerAsync("holder-flags");

        var seen = await JsonOfAsync(await CallAsync(http, HttpMethod.Get, _fixture.PluginId, "ping"));

        var names = seen.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ToList();
        names.Should().Contain(nameof(ClientPermission.ViewPayInvoices)).And.Contain(nameof(ClientPermission.PlaceNewOrders));
        names.Should().NotContain(nameof(ClientPermission.All)).And.NotContain(nameof(ClientPermission.None));
    }

    /// <summary>A client route that declares no permission is for the account holder alone, as an action that names none would be.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ARouteWithoutAPermission_IsForTheAccountHolderAlone()
    {
        var (owner, ownerHttp) = await OwnerAsync("no-permission-route");
        var sub = await SubUserAsync("no-permission-route-sub", owner.ClientId, ClientPermission.ViewPayInvoices);

        var refused = await CallAsync(sub, HttpMethod.Post, _fixture.PluginId, "notes");
        var served = await CallAsync(ownerHttp, HttpMethod.Post, _fixture.PluginId, "notes");

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(refused, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
        served.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A route marked <c>Any</c> serves a linked user who holds no permission at all.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnAnyRoute_ServesAUserWhoHoldsNoPermission()
    {
        var (owner, _) = await OwnerAsync("any-route");
        var sub = await SubUserAsync("any-route-sub", owner.ClientId, ClientPermission.None);

        var response = await CallAsync(sub, HttpMethod.Get, _fixture.PluginId, "ping");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonOfAsync(response)).GetProperty("permissions").GetArrayLength().Should().Be(0);
    }

    /// <summary>
    /// An identity plugin member who can view but not buy is held to the same permissions as a sub-user: the route that
    /// needs invoices is refused, the one that needs nothing is served, and the member acts for the account's client,
    /// which the plugin sees as the very reference the account holder has.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnIdentityPluginMemberWhoCanViewButNotBuy_IsHeldToTheMappedPermissions()
    {
        var (owner, ownerHttp) = await OwnerAsync("plugin-member");
        var accountId = await PluginMemberSeeds.LinkAccountAsync(_fixture.Factory, owner.ClientId);
        using var member = await MemberAsync("plugin-member-view", accountId, canPurchase: false);

        var invoices = await CallAsync(member.Client, HttpMethod.Get, _fixture.PluginId, "invoices/1");
        var ping = await CallAsync(member.Client, HttpMethod.Get, _fixture.PluginId, "ping");
        var holderPing = await CallAsync(ownerHttp, HttpMethod.Get, _fixture.PluginId, "ping");

        invoices.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(invoices, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
        ping.StatusCode.Should().Be(HttpStatusCode.OK);
        var seenMember = await JsonOfAsync(ping);
        var seenHolder = await JsonOfAsync(holderPing);
        seenMember.GetProperty("clientRef").GetString().Should().Be(seenHolder.GetProperty("clientRef").GetString());
        seenMember.GetProperty("userRef").GetString().Should().NotBe(seenHolder.GetProperty("userRef").GetString());
        seenMember.GetProperty("permissions").EnumerateArray().Select(p => p.GetString())
            .Should().NotContain(nameof(ClientPermission.ViewPayInvoices));
    }

    /// <summary>An identity plugin member who can purchase holds the invoice permission and is served.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnIdentityPluginMemberWhoCanBuy_IsServedTheInvoiceRoute()
    {
        var (owner, _) = await OwnerAsync("plugin-member-buy");
        var accountId = await PluginMemberSeeds.LinkAccountAsync(_fixture.Factory, owner.ClientId);
        using var member = await MemberAsync("plugin-member-buyer", accountId, canPurchase: true);

        var response = await CallAsync(member.Client, HttpMethod.Get, _fixture.PluginId, "invoices/1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>An identity plugin member whose external account has no client yet acts for nobody: 404 with the profile code.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnIdentityPluginMemberWithNoClientYet_ActsForNoAccount()
    {
        using var member = await MemberAsync("plugin-member-no-client", $"acct-{Guid.NewGuid():N}", canPurchase: true);

        var response = await CallAsync(member.Client, HttpMethod.Get, _fixture.PluginId, "ping");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_PROFILE_NOT_FOUND);
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>When the identity plugin cannot say who the caller is the answer is the retryable 503, and no plugin code runs.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task WhenTheIdentityPluginCannotAnswer_TheAnswerIs503()
    {
        var registered = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "identity-unavailable");
        using var derived = _fixture.Derive(services =>
            services.AddSingleton<IIdentityPluginResolver>(new ConflictingIdentityPluginResolver()));
        using var http = AppPluginFixture.WithBearerOf(derived, registered.Client);

        var response = await CallAsync(http, HttpMethod.Get, _fixture.PluginId, "ping");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.IDENTITY_PLUGIN_UNAVAILABLE);
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>A caller with no session is asked to sign in, and a staff account that is not a client user is refused by the portal policy.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnAnonymousCaller_Answers401_AndAStaffAccountAnswers403()
    {
        using var anonymous = _fixture.Host.CreateClient();
        using var admin = await _fixture.AdminClientAsync();

        var signedOut = await CallAsync(anonymous, HttpMethod.Get, _fixture.PluginId, "ping");
        var staff = await CallAsync(admin, HttpMethod.Get, _fixture.PluginId, "ping");

        signedOut.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        staff.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>A body over the cap is refused 413 from its declared length, and the plugin is not called.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ABodyOverTheCap_Answers413WithoutCallingThePlugin()
    {
        var (_, http) = await OwnerAsync("body-over-cap");

        var response = await CallAsync(
            http, HttpMethod.Post, _fixture.PluginId, "echo", new StringContent(new string('x', AppPluginFixture.MaxBodyBytes + 1)));

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REQUEST_TOO_LARGE);
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>A chunked body that declares no length is counted as it is read and refused once it passes the cap.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AChunkedBodyOverTheCap_Answers413()
    {
        var (_, http) = await OwnerAsync("chunked-over-cap");

        var response = await CallAsync(
            http, HttpMethod.Post, _fixture.PluginId, "echo", new UnknownLengthContent(new byte[AppPluginFixture.MaxBodyBytes + 1]));

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REQUEST_TOO_LARGE);
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>A body exactly at the cap is passed whole, with its media type.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ABodyAtTheCap_ReachesThePluginWhole()
    {
        var (_, http) = await OwnerAsync("body-at-cap");
        var text = new string('y', AppPluginFixture.MaxBodyBytes);

        var response = await CallAsync(http, HttpMethod.Post, _fixture.PluginId, "echo", new StringContent(text, Encoding.UTF8, "text/plain"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var seen = await JsonOfAsync(response);
        seen.GetProperty("body").GetString().Should().Be(text);
        seen.GetProperty("contentType").GetString().Should().StartWith("text/plain");
    }

    /// <summary>The query string, the language of the request and a literal route over a parameter one reach the plugin as the SDK describes.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TheQueryTheLanguageAndTheMostSpecificRoute_ReachThePlugin()
    {
        var (_, http) = await OwnerAsync("call-shape");
        http.DefaultRequestHeaders.Add("X-Language", "ru");

        var literal = await JsonOfAsync(await CallAsync(http, HttpMethod.Get, _fixture.PluginId, "items/latest?tag=a&tag=b&page=2"));
        var parameter = await JsonOfAsync(await CallAsync(http, HttpMethod.Get, _fixture.PluginId, "Items/Other"));

        literal.GetProperty("route").GetString().Should().Be("/items/latest");
        literal.GetProperty("routeValues").EnumerateObject().ToList().Should().BeEmpty();
        literal.GetProperty("language").GetString().Should().Be("ru");
        literal.GetProperty("query").GetProperty("tag").EnumerateArray().Select(v => v.GetString()).Should().Equal("a", "b");
        literal.GetProperty("query").GetProperty("page").EnumerateArray().Select(v => v.GetString()).Should().Equal("2");
        parameter.GetProperty("route").GetString().Should().Be("/items/{name}");
        parameter.GetProperty("routeValues").GetProperty("name").GetString().Should().Be("Other");
    }

    /// <summary>
    /// A state-changing request that rides on the session cookie without the header is refused 403 with the CSRF code
    /// before the plugin is considered; the same request with the header or a bearer token is not.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ACookieWriteWithoutTheHeader_IsRefusedAsForgery()
    {
        using var http = _fixture.Host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, Url(_fixture.PluginId, "echo"));
        request.Headers.Add("Cookie", $"{LocalSessionCookie.Name}=not-a-real-token");

        var response = await http.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(response, SharedErrorCodes.CsrfHeaderMissing);
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>A bearer token needs no header for a write: the same POST that a cookie would need the header for is served.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ABearerWriteWithoutTheHeader_IsServed()
    {
        var (_, http) = await OwnerAsync("bearer-write");

        var response = await CallAsync(http, HttpMethod.Post, _fixture.PluginId, "echo", new StringContent("x"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A plugin's JSON keeps the status it chose and is written in camelCase; a plugin's empty success is 204.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task JsonAndNoContent_PassThroughWithThePluginsStatus()
    {
        var (_, http) = await OwnerAsync("pass-through");

        _fixture.Probe.Behaviour = _ => PluginReply.Json(new { InvoiceNumber = "INV-7" }, 201);
        var created = await CallAsync(http, HttpMethod.Get, _fixture.PluginId, "ping");
        _fixture.Probe.Behaviour = _ => PluginReply.NoContent();
        var empty = await CallAsync(http, HttpMethod.Get, _fixture.PluginId, "ping");

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        (await JsonOfAsync(created)).GetProperty("invoiceNumber").GetString().Should().Be("INV-7");
        empty.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await empty.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    /// <summary>A plugin's bare string is still a JSON document, whatever the caller says it accepts.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ABareStringReply_IsWrittenAsJson()
    {
        var (_, http) = await OwnerAsync("bare-string");
        http.DefaultRequestHeaders.Accept.ParseAdd("text/plain");
        _fixture.Probe.Behaviour = _ => PluginReply.Json("hello");

        var response = await CallAsync(http, HttpMethod.Get, _fixture.PluginId, "ping");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        (await response.Content.ReadAsStringAsync()).Should().Be("\"hello\"");
    }

    /// <summary>
    /// A plugin's problem becomes the standard error body with the plugin's status and sentence, and its code is namespaced
    /// under the plugin's id, so it is not a host code.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AProblem_BecomesTheStandardErrorBodyWithANamespacedCode()
    {
        var (_, http) = await OwnerAsync("problem");
        _fixture.Probe.Behaviour = _ => PluginReply.Problem(409, "invoice_locked", "This invoice is locked.");

        var response = await CallAsync(http, HttpMethod.Get, _fixture.PluginId, "ping");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ErrorBody.ContentType);
        var body = await JsonOfAsync(response);
        body.GetProperty("type").GetString().Should().Be($"plugin.{_fixture.PluginId}.invoice_locked");
        body.GetProperty("title").GetString().Should().Be("Conflict");
        body.GetProperty("status").GetInt32().Should().Be(409);
        body.GetProperty("detail").GetString().Should().Be("This invoice is locked.");
    }

    /// <summary>A plugin cannot answer with a host code: whatever it writes sits under its own namespace.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AProblemThatNamesAHostCode_CannotImpersonateIt()
    {
        var (_, http) = await OwnerAsync("impersonation");
        _fixture.Probe.Behaviour = _ => PluginReply.Problem(403, HostpanelErrorCodes.CLIENT_PERMISSION_DENIED, "Pretend.");

        var response = await CallAsync(http, HttpMethod.Get, _fixture.PluginId, "ping");

        var type = (await JsonOfAsync(response)).GetProperty("type").GetString();
        type.Should().NotBe(HostpanelErrorCodes.CLIENT_PERMISSION_DENIED);
        type.Should().Be($"plugin.{_fixture.PluginId}.{HostpanelErrorCodes.CLIENT_PERMISSION_DENIED}");
    }

    /// <summary>A problem with no sentence gets the host's own, in the caller's language; a long sentence is cut.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AProblemWithoutADetail_GetsALocalisedSentence_AndALongOneIsCut()
    {
        var (_, http) = await OwnerAsync("problem-detail");
        http.DefaultRequestHeaders.Add("X-Language", "ru");

        _fixture.Probe.Behaviour = _ => PluginReply.Problem(400, "bad_input");
        var bare = await JsonOfAsync(await CallAsync(http, HttpMethod.Get, _fixture.PluginId, "ping"));
        _fixture.Probe.Behaviour = _ => PluginReply.Problem(400, "bad_input", new string('z', PluginCallRunner.DetailMaxLength + 100));
        var cut = await JsonOfAsync(await CallAsync(http, HttpMethod.Get, _fixture.PluginId, "ping"));

        bare.GetProperty("detail").GetString().Should().MatchRegex("[А-Яа-яЁё]");
        cut.GetProperty("detail").GetString()!.Length.Should().Be(PluginCallRunner.DetailMaxLength);
    }

    /// <summary>A problem whose code is not a plain code is a plugin fault, not a header or a line the plugin gets to write.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AProblemWithAMalformedCode_IsAPluginFailure()
    {
        var (_, http) = await OwnerAsync("malformed-code");
        _fixture.Probe.Behaviour = _ => PluginReply.Problem(400, "Not a code!\nsecond-line", "x");

        var response = await CallAsync(http, HttpMethod.Get, _fixture.PluginId, "ping");

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_FAILED);
    }

    /// <summary>A plugin that throws is answered <c>PLUGIN_FAILED</c> with no message and no stack, and the failure is logged with the plugin id.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnExceptionInThePlugin_BecomesPluginFailedWithoutLeakingAnything()
    {
        var (_, http) = await OwnerAsync("exception");
        _fixture.Probe.Behaviour = _ => throw new InvalidOperationException("secret-detail-of-the-plugin");

        var response = await CallAsync(http, HttpMethod.Get, _fixture.PluginId, "ping");

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_FAILED);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().NotContainAny(LeakMarkers);
        _fixture.LoggedFailureOf(_fixture.PluginId).Should().BeTrue();
    }

    /// <summary>A plugin that cannot even be built is a plugin failure too, and its constructor's message goes nowhere near the caller.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task APluginWhoseConstructorThrows_BecomesPluginFailed()
    {
        var (_, http) = await OwnerAsync("constructor");

        var response = await CallAsync(http, HttpMethod.Get, _fixture.BrokenPluginId, "ping");

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_FAILED);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(ThrowingConstructorAppPlugin.Secret);
        _fixture.LoggedFailureOf(_fixture.BrokenPluginId).Should().BeTrue();
    }

    /// <summary>
    /// The host does not share client facts or keep plugin data yet: a plugin that asks for them fails visibly, as a plugin
    /// failure, instead of reading an empty profile or losing a write.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task APluginThatAsksForHostFactsOrTheStore_FailsVisibly()
    {
        var (_, http) = await OwnerAsync("unavailable-services");

        _fixture.Probe.Behaviour = call => PluginReply.Json(call.Host.GetProfileAsync(CancellationToken.None).GetAwaiter().GetResult()!);
        var host = await CallAsync(http, HttpMethod.Get, _fixture.PluginId, "ping");
        _fixture.Probe.Behaviour = call =>
        {
            call.Store.SetAsync("k", "v", CancellationToken.None).GetAwaiter().GetResult();
            return PluginReply.NoContent();
        };
        var store = await CallAsync(http, HttpMethod.Get, _fixture.PluginId, "ping");

        host.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        store.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        await ClientsTestSeeds.AssertCodeAsync(host, HostpanelErrorCodes.PLUGIN_FAILED);
    }

    /// <summary>A route that streams is refused 501 before the plugin is called, because the host cannot forward a stream yet.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AStreamingRoute_Answers501WithoutCallingThePlugin()
    {
        var (_, http) = await OwnerAsync("streaming-route");

        var response = await CallAsync(http, HttpMethod.Get, _fixture.PluginId, "stream");

        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_STREAMING_NOT_SUPPORTED);
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>A streamed reply from a route that did not declare streaming is the same 501, and the stream is never enumerated.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AStreamedReply_Answers501AndIsNeverEnumerated()
    {
        var (_, http) = await OwnerAsync("streamed-reply");
        var enumerated = false;
        _fixture.Probe.Behaviour = _ => PluginReply.Stream(Events(() => enumerated = true));

        var response = await CallAsync(http, HttpMethod.Get, _fixture.PluginId, "ping");

        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_STREAMING_NOT_SUPPORTED);
        enumerated.Should().BeFalse();
    }

    /// <summary>An event source that notes when it is enumerated.</summary>
    /// <param name="onEnumerated">Runs when the first event is asked for.</param>
    /// <returns>An empty stream.</returns>
    private static async IAsyncEnumerable<PluginEvent> Events(Action onEnumerated)
    {
        onEnumerated();
        await Task.CompletedTask;
        yield break;
    }

    /// <summary>
    /// The references a plugin sees are stable for one plugin and one client, differ for another plugin and for another
    /// client, and are neither the host's ids nor reversible by looking at them.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task References_AreStablePerPluginAndDifferAcrossPluginsAndClients()
    {
        var (owner, http) = await OwnerAsync("references-one");
        var (_, otherHttp) = await OwnerAsync("references-two");

        var first = await JsonOfAsync(await CallAsync(http, HttpMethod.Get, _fixture.PluginId, "ping"));
        var again = await JsonOfAsync(await CallAsync(http, HttpMethod.Get, _fixture.PluginId, "ping"));
        var elsewhere = await JsonOfAsync(await CallAsync(http, HttpMethod.Get, _fixture.OtherPluginId, "ping"));
        var anotherClient = await JsonOfAsync(await CallAsync(otherHttp, HttpMethod.Get, _fixture.PluginId, "ping"));

        foreach (var member in new[] { "clientRef", "userRef" })
        {
            var reference = first.GetProperty(member).GetString()!;
            OpaqueReference().IsMatch(reference).Should().BeTrue();
            again.GetProperty(member).GetString().Should().Be(reference);
            elsewhere.GetProperty(member).GetString().Should().NotBe(reference);
            anotherClient.GetProperty(member).GetString().Should().NotBe(reference);
        }

        first.GetProperty("clientRef").GetString().Should().NotBe(first.GetProperty("userRef").GetString());
        first.GetProperty("userRef").GetString().Should().NotContain(owner.UserId);
        first.GetProperty("clientRef").GetString().Should().NotBe(owner.ClientId.ToString());
    }

    /// <summary>
    /// A call that changes state is audited with the plugin id, the declared route, the client and the caller, and never
    /// the body; a read is not audited, and neither is a call that is refused.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnUnsafeCall_IsAuditedWithoutItsBody_AndReadsAndRefusalsAreNot()
    {
        var (owner, http) = await OwnerAsync("audit");
        var sub = await SubUserAsync("audit-sub", owner.ClientId, ClientPermission.ViewDomains);

        await CallAsync(http, HttpMethod.Get, _fixture.PluginId, "invoices/1");
        await CallAsync(sub, HttpMethod.Post, _fixture.PluginId, "invoices/1/pay", new StringContent("card-number-4242"));
        await CallAsync(http, HttpMethod.Post, _fixture.PluginId, "echo", new StringContent(new string('q', AppPluginFixture.MaxBodyBytes + 1)));
        var served = await CallAsync(http, HttpMethod.Post, _fixture.PluginId, "invoices/1/pay", new StringContent("card-number-4242"));

        served.StatusCode.Should().Be(HttpStatusCode.OK);
        // The other tests of the class call the same plugin as other clients, so only this client's lines are the test's own.
        var lines = (await _fixture.ActivityOfAsync(_fixture.PluginId)).Where(l => l.ClientId == owner.ClientId).ToList();
        var line = lines.Should().ContainSingle("a read, a refused permission and an oversized body leave no line").Subject;
        line.ClientId.Should().Be(owner.ClientId);
        line.Description.Should().Contain($"Plugin ID: {_fixture.PluginId}").And.Contain("Area: Client").And.Contain("POST /invoices/{id}/pay");
        line.Description.Should().NotContain("card-number");
    }

    /// <summary>Starts a host with an identity plugin that vouches for a new user as a member of an external account.</summary>
    /// <param name="label">Distinguishes the created address.</param>
    /// <param name="accountId">The external account the plugin maps the member to.</param>
    /// <param name="canPurchase">Whether the plugin lets the member purchase.</param>
    /// <returns>The member and the derived host that serves them, to dispose when the test ends.</returns>
    private async Task<PluginMember> MemberAsync(string label, string accountId, bool canPurchase)
    {
        var user = await ClientsTestSeeds.CreateUnlinkedUserAsync(_fixture.Factory, label);
        var plugin = new StubClientResolver();
        plugin.Grant(
            user.UserId,
            new ClientAccess(new ExternalAccount(PluginMemberSeeds.Provider, accountId, null, null), canPurchase, CanManagePaymentMethods: false));
        var derived = _fixture.Derive(services => services.AddSingleton<IIdentityPluginResolver>(new FakeIdentityPluginResolver(plugin)));

        return new PluginMember(derived, AppPluginFixture.WithBearerOf(derived, user.Client), 0, user.Email);
    }
}
