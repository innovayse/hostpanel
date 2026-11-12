using System.Net;
using System.Text;
using Hostpanel.Sdk.Plugins.Endpoints;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Manifest;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// A callback the plugin declares under <c>api.callbacks</c> reaches the plugin through the public door, with no sign-in,
/// the body byte for byte and the headers as sent; a callback of a disabled plugin, an undeclared path and a wrong method
/// answer the same 404; a body over the cap is refused with 413 before the plugin runs; a raw reply is written as the plugin
/// chose it, with its status and media type, unless it is over the cap or has no valid media type.
/// </summary>
public sealed class CallPluginCallbackDeclaredTests : IAsyncLifetime
{
    /// <summary>The body cap of the host under test.</summary>
    private const int MaxBodyBytes = 64;

    /// <summary>The one origin of the test plugins' manifests that a page may reach; the other two entries are not exact https origins.</summary>
    private const string GatewayOrigin = "https://js.gateway.example";

    /// <summary>The database and the host.</summary>
    private readonly IntegrationTestFactory _factory = new();

    /// <summary>What the plugin saw.</summary>
    private readonly AppPluginProbe _probe = new();

    /// <summary>The id of the enabled plugin that declares the callbacks.</summary>
    private readonly string _pluginId = PluginActivationTestSeeds.NewId("cb");

    /// <summary>The id of a plugin that declares the same callbacks and is not enabled.</summary>
    private readonly string _disabledId = PluginActivationTestSeeds.NewId("cboff");

    /// <summary>The id of an enabled payment plugin that declares the <c>endpoints</c> capability, the callback, a client route and an admin route.</summary>
    private readonly string _paymentId = PluginActivationTestSeeds.NewId("cbpay");

    /// <summary>The id of an enabled payment plugin that declares the same routes but not the <c>endpoints</c> capability.</summary>
    private readonly string _paymentWithoutCapabilityId = PluginActivationTestSeeds.NewId("cbnocap");

    /// <summary>The host with the plugins loaded.</summary>
    private WebApplicationFactory<Program> _host = null!;

    /// <summary>Starts the database and the host.</summary>
    /// <returns>A task that completes when the host is ready.</returns>
    public async Task InitializeAsync()
    {
        await _factory.InitializeAsync();

        var registry = PluginActivationTestSeeds.Registry(
            Declaring(_pluginId),
            Declaring(_disabledId),
            Declaring(_paymentId, PluginType.Payment, [PluginCapabilities.Endpoints]),
            Declaring(_paymentWithoutCapabilityId, PluginType.Payment, []));
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await PluginActivationTestSeeds.EnableAsync(db, _pluginId, _paymentId, _paymentWithoutCapabilityId);
        }

        _host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<PluginRegistry>();
            services.AddSingleton(registry);
            services.AddSingleton(_probe);
            services.Configure<PluginApiOptions>(options => options.MaxBodyBytes = MaxBodyBytes);
        }));
    }

    /// <summary>Releases the host and the database.</summary>
    /// <returns>A task that completes when both are released.</returns>
    public async Task DisposeAsync()
    {
        _host.Dispose();
        await _factory.DisposeAsync();
    }

    /// <summary>
    /// Loads a test plugin whose manifest declares one callback, <c>POST /pay/{order}</c>, a client route <c>GET /ping</c> and
    /// an admin route <c>GET /stats</c>.
    /// </summary>
    /// <param name="id">The plugin id.</param>
    /// <param name="type">The plugin type the manifest declares.</param>
    /// <param name="capabilities">The capabilities the manifest lists.</param>
    /// <returns>The loaded plugin.</returns>
    private static LoadedPlugin Declaring(string id, PluginType type = PluginType.App, IReadOnlyList<string>? capabilities = null)
    {
        var loaded = PluginActivationTestSeeds.Plugin(id, type, typeof(FakeAppPlugin));
        var manifest = new PluginManifest
        {
            Id = loaded.Manifest.Id,
            Name = loaded.Manifest.Name,
            Version = loaded.Manifest.Version,
            Author = loaded.Manifest.Author,
            Description = loaded.Manifest.Description,
            Type = type,
            Category = loaded.Manifest.Category,
            Capabilities = capabilities ?? [],
            ExternalOrigins = [GatewayOrigin, "https://*.wildcard.example", "http://plain.example"],
            EntryPoint = loaded.Manifest.EntryPoint,
            SdkVersion = loaded.Manifest.SdkVersion,
            Api = new PluginApi
            {
                Callbacks = [new PluginRoute { Method = PluginRouteMethod.Post, Path = "/pay/{order}" }],
                Client = [new PluginRoute { Method = PluginRouteMethod.Get, Path = "/ping", Permission = PluginPermissions.Any }],
                Admin = [new PluginRoute { Method = PluginRouteMethod.Get, Path = "/stats" }],
            },
        };

        return new LoadedPlugin(manifest, typeof(FakeAppPlugin), loaded.Views);
    }

    /// <summary>A declared callback reaches the plugin with no sign-in, and the raw body arrives byte for byte.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ADeclaredCallback_ReachesThePlugin_WithTheRawBodyIntact()
    {
        using var http = _host.CreateClient();

        // Spacing, key order and a trailing newline are what a signature over the body would break on if the host re-serialised it.
        var raw = "{ \"id\" :\"evt_1\",\n\"amount\":  1200 }\n";
        using var content = new StringContent(raw, Encoding.UTF8, "application/json");
        content.Headers.Add("X-Signature", "t=1,v1=abc");
        using var response = await http.PostAsync($"/api/plugins/{_pluginId}/callback/pay/o-7?x=1", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var call = _probe.Calls.Should().ContainSingle().Subject;
        Encoding.UTF8.GetString(call.Body.Span).Should().Be(raw);
        call.Area.Should().Be(PluginArea.Callback);
        call.RouteValues["order"].Should().Be("o-7");
        call.Query["x"].Should().Equal("1");
        call.Caller.ClientRef.Should().BeNull();
        call.Caller.Permissions.Should().BeEmpty();
        call.Headers["X-Signature"].Should().Equal("t=1,v1=abc");
    }

    /// <summary>A payment plugin that declares the <c>endpoints</c> capability receives its declared callback, told it came through the callback door.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task APaymentPluginDeclaringTheEndpointsCapability_ReceivesItsDeclaredCallback()
    {
        using var http = _host.CreateClient();

        using var response = await http.PostAsync($"/api/plugins/{_paymentId}/callback/pay/o-9", new StringContent("{}"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var call = _probe.Calls.Should().ContainSingle().Subject;
        call.Area.Should().Be(PluginArea.Callback);
        call.RouteValues["order"].Should().Be("o-9");
    }

    /// <summary>A payment plugin that does not declare the capability gets the uniform 404 for the same callback, byte for byte as an unknown plugin.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task APaymentPluginWithoutTheEndpointsCapability_AnswersTheUniform404()
    {
        using var http = _host.CreateClient();

        using var unknown = await http.PostAsync("/api/plugins/no-such-plugin/callback/pay/o-9", new StringContent("{}"));
        using var withoutCapability = await http.PostAsync(
            $"/api/plugins/{_paymentWithoutCapabilityId}/callback/pay/o-9", new StringContent("{}"));

        withoutCapability.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await withoutCapability.Content.ReadAsStringAsync()).Should().Be(await unknown.Content.ReadAsStringAsync());
        _probe.Calls.Should().BeEmpty();
    }

    /// <summary>
    /// The <c>endpoints</c> capability opens the client and admin routes of a payment plugin as it does for any non-app type:
    /// a plugin that declares it answers a signed-in client and an administrator, and one that does not stays a uniform 404
    /// for both and is never built.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ThePaymentPluginsClientAndAdminRoutes_FollowTheEndpointsCapability()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_factory, "cb-payment-client");
        using var client = AppPluginFixture.WithBearerOf(_host, owner.Client);
        using var admin = _host.CreateClient();
        admin.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await _factory.GetAdminTokenAsync());

        using var withoutClientCall = await client.GetAsync($"/api/client/plugins/{_paymentWithoutCapabilityId}/ping");
        using var withoutAdminCall = await admin.GetAsync($"/api/admin/plugins/{_paymentWithoutCapabilityId}/ext/stats");

        withoutClientCall.StatusCode.Should().Be(HttpStatusCode.NotFound);
        withoutAdminCall.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await withoutClientCall.Content.ReadAsStringAsync()).Should().Contain(HostpanelErrorCodes.PLUGIN_ROUTE_NOT_FOUND);
        (await withoutAdminCall.Content.ReadAsStringAsync()).Should().Contain(HostpanelErrorCodes.PLUGIN_ROUTE_NOT_FOUND);
        _probe.Constructed.Should().Be(0);
        _probe.Calls.Should().BeEmpty();

        using var clientCall = await client.GetAsync($"/api/client/plugins/{_paymentId}/ping");
        using var adminCall = await admin.GetAsync($"/api/admin/plugins/{_paymentId}/ext/stats");

        clientCall.StatusCode.Should().Be(HttpStatusCode.OK);
        adminCall.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Only the allow-listed headers reach the plugin: credentials, the proxy's forwarded headers and the host's session headers do not.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ADeclaredCallback_ReceivesOnlyAllowListedHeaders()
    {
        using var http = _host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/plugins/{_pluginId}/callback/pay/o-7")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        request.Headers.TryAddWithoutValidation("Cookie", "session=secret");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer secret");
        request.Headers.TryAddWithoutValidation("Proxy-Authorization", "Basic secret");
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.9");
        request.Headers.TryAddWithoutValidation("X-Language", "ru");
        request.Headers.TryAddWithoutValidation("X-Gateway-Event", "paid");
        request.Headers.TryAddWithoutValidation("User-Agent", "gateway/1");

        using var response = await http.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var headers = _probe.Calls.Should().ContainSingle().Subject.Headers;
        headers.Keys.Should().Contain(["Content-Type", "User-Agent", "X-Gateway-Event"]);
        headers.Keys.Should().NotContain(["Cookie", "Authorization", "Proxy-Authorization", "X-Forwarded-For", "X-Language", "Host"]);
    }

    /// <summary>The header allow-list decides name by name, in any case.</summary>
    /// <param name="name">The header name.</param>
    /// <param name="expected">Whether it is passed on.</param>
    [Theory]
    [InlineData("content-type", true)]
    [InlineData("Webhook-Signature", true)]
    [InlineData("x-custom-signature", true)]
    [InlineData("X-Forwarded-Host", false)]
    [InlineData("x-forwarded-proto", false)]
    [InlineData("X-Real-IP", false)]
    [InlineData("X-Service-Key", false)]
    [InlineData("Cookie", false)]
    [InlineData("authorization", false)]
    [InlineData("Proxy-Authorization", false)]
    [InlineData("Host", false)]
    [InlineData("Accept-Language", false)]
    public void TheHeaderAllowList_DecidesByName(string name, bool expected)
    {
        Innovayse.Hostpanel.API.Plugins.PluginHttpInputReader.IsPassedOn(name).Should().Be(expected);
    }

    /// <summary>A callback of a plugin that is switched off, an undeclared path and a wrong method answer the same 404.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ADisabledUndeclaredOrWrongMethodCallback_IsTheSame404()
    {
        using var http = _host.CreateClient();
        using var unknown = await http.PostAsync("/api/plugins/no-such-plugin/callback/pay/o-7", new StringContent("{}"));
        using var disabled = await http.PostAsync($"/api/plugins/{_disabledId}/callback/pay/o-7", new StringContent("{}"));
        using var undeclared = await http.PostAsync($"/api/plugins/{_pluginId}/callback/refund/o-7", new StringContent("{}"));
        using var wrongMethod = await http.GetAsync($"/api/plugins/{_pluginId}/callback/pay/o-7");

        var expected = await unknown.Content.ReadAsStringAsync();
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        expected.Should().Contain(HostpanelErrorCodes.PLUGIN_ROUTE_NOT_FOUND);
        foreach (var refusal in new[] { disabled, undeclared, wrongMethod })
        {
            refusal.StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await refusal.Content.ReadAsStringAsync()).Should().Be(expected);
        }

        _probe.Calls.Should().BeEmpty();
    }

    /// <summary>A body over the cap on a declared callback is refused with 413 and the plugin never runs.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ADeclaredCallback_WithAnOversizedBody_IsRefusedWith413()
    {
        using var http = _host.CreateClient();
        using var response = await http.PostAsync(
            $"/api/plugins/{_pluginId}/callback/pay/o-7", new StringContent(new string('x', MaxBodyBytes * 2)));

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        _probe.Calls.Should().BeEmpty();
    }

    /// <summary>A raw reply to a callback is written byte for byte with the plugin's status and media type, and is not sniffed.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ARawReply_IsWrittenAsTheBytesStatusAndMediaTypeThePluginChose()
    {
        _probe.Behaviour = _ => PluginReply.Raw(202, Encoding.UTF8.GetBytes("[accepted]"), "text/plain; charset=utf-8");
        using var http = _host.CreateClient();

        using var response = await http.PostAsync($"/api/plugins/{_pluginId}/callback/pay/o-7", new StringContent("{}"));

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        response.Content.Headers.ContentType!.ToString().Should().Be("text/plain; charset=utf-8");
        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        response.Headers.GetValues("Content-Security-Policy").Should().Equal("default-src 'none'; frame-ancestors 'none'");
        (await response.Content.ReadAsStringAsync()).Should().Be("[accepted]");
    }

    /// <summary>A raw reply of an allowed media type, or an HTML page, is written; one outside the list is a plugin fault, so a callback cannot serve script or other markup.</summary>
    /// <param name="mediaType">The media type the plugin chose.</param>
    /// <param name="allowed">Whether the host writes it.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("text/plain", true)]
    [InlineData("application/json; charset=utf-8", true)]
    [InlineData("APPLICATION/XML", true)]
    [InlineData("application/x-www-form-urlencoded", true)]
    [InlineData("text/html; charset=utf-8", true)]
    [InlineData("image/svg+xml", false)]
    [InlineData("application/javascript", false)]
    [InlineData("application/octet-stream", false)]
    public async Task ARawReply_IsWrittenOnlyForAnAllowedMediaType(string mediaType, bool allowed)
    {
        _probe.Behaviour = _ => PluginReply.Raw(200, Encoding.UTF8.GetBytes("OK"), mediaType);
        using var http = _host.CreateClient();

        using var response = await http.PostAsync($"/api/plugins/{_pluginId}/callback/pay/o-7", new StringContent("{}"));

        response.StatusCode.Should().Be(allowed ? HttpStatusCode.OK : HttpStatusCode.BadGateway);
        if (!allowed)
        {
            (await response.Content.ReadAsStringAsync()).Should().Contain(HostpanelErrorCodes.PLUGIN_FAILED);
        }
    }

    /// <summary>
    /// An HTML page a callback answers is written as the plugin chose it, under a strict policy built from the exact https
    /// origins its manifest declares: nothing loads or connects anywhere else, the page cannot be framed, it has no base
    /// address and runs sandboxed without this application's origin, and it is never cached and sends no referrer.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnHtmlReply_IsWrittenAsAPage_UnderAStrictPolicyOfTheDeclaredOrigins()
    {
        const string page = "<!doctype html><title>Pay</title><p>Pay</p>";
        _probe.Behaviour = _ => PluginReply.Raw(200, Encoding.UTF8.GetBytes(page), "text/html; charset=utf-8");
        using var http = _host.CreateClient();

        using var posted = await http.PostAsync($"/api/plugins/{_pluginId}/callback/pay/o-7", new StringContent("{}"));

        posted.StatusCode.Should().Be(HttpStatusCode.OK);
        posted.Content.Headers.ContentType!.ToString().Should().Be("text/html; charset=utf-8");
        (await posted.Content.ReadAsStringAsync()).Should().Be(page);
        posted.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        posted.Headers.CacheControl!.NoStore.Should().BeTrue();
        posted.Headers.GetValues("Referrer-Policy").Should().Equal("no-referrer");
        var policy = posted.Headers.GetValues("Content-Security-Policy").Single();
        policy.Should().Be(RawReplyResult.PagePolicy([GatewayOrigin]));
        policy.Should().Contain("default-src 'none'").And.Contain($"script-src 'unsafe-inline' {GatewayOrigin};")
            .And.Contain($"connect-src {GatewayOrigin};").And.Contain("base-uri 'none'").And.Contain("frame-ancestors 'none'")
            .And.Contain("sandbox allow-scripts allow-forms");
        policy.Should().NotContain("wildcard").And.NotContain("plain.example").And.NotContain("'self'").And.NotContain("*");
    }

    /// <summary>A page whose plugin declares no origin may reach none: every source directive that lists origins says <c>'none'</c>.</summary>
    [Fact]
    public void APagePolicy_WithNoOrigins_AllowsNoOrigin()
    {
        var policy = RawReplyResult.PagePolicy([]);

        policy.Should().Contain("connect-src 'none'").And.Contain("frame-src 'none'").And.Contain("form-action 'none'")
            .And.Contain("font-src 'none'").And.Contain("script-src 'unsafe-inline';").And.Contain("img-src data:;");
        policy.Should().NotContain("https:");
    }

    /// <summary>A raw reply with an empty body and a refusing status answers that status with no body.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnEmptyRawReply_WithStatus403_AnswersItWithNoBody()
    {
        _probe.Behaviour = _ => PluginReply.Raw(403, ReadOnlyMemory<byte>.Empty, "text/plain");
        using var http = _host.CreateClient();

        using var response = await http.PostAsync($"/api/plugins/{_pluginId}/callback/pay/o-7", new StringContent("{}"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsByteArrayAsync()).Should().BeEmpty();
    }

    /// <summary>A raw reply over the body cap is a plugin fault, so a plugin cannot make the host send an unbounded body.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ARawReply_OverTheCap_IsAPluginFailure()
    {
        _probe.Behaviour = _ => PluginReply.Raw(200, new byte[MaxBodyBytes + 1], "application/octet-stream");
        using var http = _host.CreateClient();

        using var response = await http.PostAsync($"/api/plugins/{_pluginId}/callback/pay/o-7", new StringContent("{}"));

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await response.Content.ReadAsStringAsync()).Should().Contain(HostpanelErrorCodes.PLUGIN_FAILED);
    }

    /// <summary>A raw reply whose media type does not parse is a plugin fault.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ARawReply_WithAnUnparsableMediaType_IsAPluginFailure()
    {
        _probe.Behaviour = _ => PluginReply.Raw(200, Encoding.UTF8.GetBytes("OK"), "not a media type");
        using var http = _host.CreateClient();

        using var response = await http.PostAsync($"/api/plugins/{_pluginId}/callback/pay/o-7", new StringContent("{}"));

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await response.Content.ReadAsStringAsync()).Should().Contain(HostpanelErrorCodes.PLUGIN_FAILED);
    }
}
