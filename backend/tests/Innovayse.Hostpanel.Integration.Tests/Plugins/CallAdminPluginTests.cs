using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.SDK.Plugins.Endpoints;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// The admin area door to an app plugin (<c>CallAdminPluginCommand</c> through <c>AdminPluginCallsController</c>),
/// against Postgres and the test-only plugins of <see cref="AppPluginFixture"/>: the admin policy is the whole
/// authorisation, the plugin sees the administrator as an opaque reference with no client, and the refusals and
/// replies are the same as in the client area.
/// </summary>
public sealed class CallAdminPluginTests : IClassFixture<AppPluginFixture>
{
    /// <summary>The host, the database and the plugins.</summary>
    private readonly AppPluginFixture _fixture;

    /// <summary>Initializes the test, which starts from a probe that has seen nothing and echoes.</summary>
    /// <param name="fixture">The host, the database and the plugins.</param>
    public CallAdminPluginTests(AppPluginFixture fixture)
    {
        _fixture = fixture;
        _fixture.Probe.Reset();
    }

    /// <summary>Sends a request to an admin area plugin route.</summary>
    /// <param name="http">The caller.</param>
    /// <param name="method">The HTTP method.</param>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="path">The path below the plugin's admin prefix.</param>
    /// <param name="content">The body, or <see langword="null"/>.</param>
    /// <returns>The response.</returns>
    private static async Task<HttpResponseMessage> CallAsync(
        HttpClient http, HttpMethod method, string pluginId, string path, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, $"/api/admin/plugins/{pluginId}/ext/{path}") { Content = content };
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

    /// <summary>An administrator is served, and the plugin sees an admin call: no client, no client permissions, an opaque user reference.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnAdministrator_IsServedAndThePluginSeesNoClient()
    {
        using var admin = await _fixture.AdminClientAsync();

        var response = await CallAsync(admin, HttpMethod.Get, _fixture.PluginId, "stats");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var seen = await JsonOfAsync(response);
        seen.GetProperty("area").GetString().Should().Be("Admin");
        seen.GetProperty("route").GetString().Should().Be("/stats");
        seen.GetProperty("clientRef").ValueKind.Should().Be(JsonValueKind.Null);
        seen.GetProperty("permissions").GetArrayLength().Should().Be(0);
        seen.GetProperty("userRef").GetString().Should().HaveLength(43);
    }

    /// <summary>The user reference of the same administrator differs for another plugin and is the same on a second call.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TheAdministratorsReference_IsStablePerPluginAndDiffersAcrossPlugins()
    {
        using var admin = await _fixture.AdminClientAsync();

        var first = await JsonOfAsync(await CallAsync(admin, HttpMethod.Get, _fixture.PluginId, "stats"));
        var again = await JsonOfAsync(await CallAsync(admin, HttpMethod.Get, _fixture.PluginId, "stats"));
        var other = await JsonOfAsync(await CallAsync(admin, HttpMethod.Get, _fixture.OtherPluginId, "stats"));

        again.GetProperty("userRef").GetString().Should().Be(first.GetProperty("userRef").GetString());
        other.GetProperty("userRef").GetString().Should().NotBe(first.GetProperty("userRef").GetString());
    }

    /// <summary>A caller who is not an administrator is refused by the policy and no plugin code runs; no session at all is 401.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ANonAdministrator_IsRefusedByTheAdminPolicy()
    {
        var client = await ClientsTestSeeds.RegisterClientAsync(_fixture.Factory, "admin-door-client");
        using var asClient = _fixture.ClientLike(client.Client);
        using var anonymous = _fixture.Host.CreateClient();

        var forbidden = await CallAsync(asClient, HttpMethod.Get, _fixture.PluginId, "stats");
        var signedOut = await CallAsync(anonymous, HttpMethod.Get, _fixture.PluginId, "stats");

        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        signedOut.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>
    /// An unknown plugin, a disabled one, a plugin of a type that serves no routes and an undeclared route answer the
    /// same 404 to an administrator, and a route declared only in the client area is not there in the admin area.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UnknownDisabledNonAppUndeclaredAndClientOnlyRoutes_AnswerTheSame404()
    {
        using var admin = await _fixture.AdminClientAsync();

        var responses = new[]
        {
            await CallAsync(admin, HttpMethod.Get, "no-such-plugin", "stats"),
            await CallAsync(admin, HttpMethod.Get, _fixture.DisabledPluginId, "stats"),
            await CallAsync(admin, HttpMethod.Get, _fixture.NotAppPluginId, "stats"),
            await CallAsync(admin, HttpMethod.Get, _fixture.PluginId, "nothing"),
            await CallAsync(admin, HttpMethod.Get, _fixture.PluginId, "ping"),
        };

        var expected = await JsonOfAsync(responses[0]);
        foreach (var response in responses)
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

    /// <summary>A body over the cap is refused 413 and the plugin is not called.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ABodyOverTheCap_Answers413()
    {
        using var admin = await _fixture.AdminClientAsync();

        var response = await CallAsync(
            admin, HttpMethod.Post, _fixture.PluginId, "echo", new StringContent(new string('x', AppPluginFixture.MaxBodyBytes + 1)));

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REQUEST_TOO_LARGE);
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>A cookie-authenticated write without the header is refused as forgery before anything else.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ACookieWriteWithoutTheHeader_IsRefusedAsForgery()
    {
        using var http = _fixture.Host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/plugins/{_fixture.PluginId}/ext/reindex");
        request.Headers.Add("Cookie", $"{LocalSessionCookie.Name}=not-a-real-token");

        var response = await http.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ClientsTestSeeds.AssertCodeAsync(response, SharedErrorCodes.CsrfHeaderMissing);
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>A plugin's problem and a plugin's exception are mapped as in the client area.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AProblemAndAnException_AreMappedAsInTheClientArea()
    {
        using var admin = await _fixture.AdminClientAsync();

        _fixture.Probe.Behaviour = _ => PluginReply.Problem(422, "index_busy", "The index is busy.");
        var problem = await CallAsync(admin, HttpMethod.Post, _fixture.PluginId, "reindex");
        _fixture.Probe.Behaviour = _ => throw new InvalidOperationException("secret-detail-of-the-plugin");
        var failure = await CallAsync(admin, HttpMethod.Get, _fixture.PluginId, "stats");

        problem.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        problem.Content.Headers.ContentType!.MediaType.Should().Be(ErrorBody.ContentType);
        (await JsonOfAsync(problem)).GetProperty("type").GetString().Should().Be($"plugin.{_fixture.PluginId}.index_busy");
        failure.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        await ClientsTestSeeds.AssertCodeAsync(failure, HostpanelErrorCodes.PLUGIN_FAILED);
        (await failure.Content.ReadAsStringAsync()).Should().NotContain("secret-detail");
    }

    /// <summary>A streaming admin route is refused 501 before the plugin is called.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AStreamingRoute_Answers501()
    {
        using var admin = await _fixture.AdminClientAsync();

        var response = await CallAsync(admin, HttpMethod.Get, _fixture.PluginId, "stream");

        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);
        await ClientsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_STREAMING_NOT_SUPPORTED);
        _fixture.Probe.Constructed.Should().Be(0);
    }

    /// <summary>An unsafe admin call is audited with the plugin id and the route and no client; a read is not.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AnUnsafeAdminCall_IsAuditedWithNoClient()
    {
        using var admin = await _fixture.AdminClientAsync();

        await CallAsync(admin, HttpMethod.Get, _fixture.OtherPluginId, "stats");
        var served = await CallAsync(admin, HttpMethod.Post, _fixture.OtherPluginId, "reindex", new StringContent("rebuild-everything"));

        served.StatusCode.Should().Be(HttpStatusCode.OK);
        var lines = await _fixture.ActivityOfAsync(_fixture.OtherPluginId);
        var line = lines.Should().ContainSingle().Subject;
        line.ClientId.Should().BeNull();
        line.Description.Should().Contain($"Plugin ID: {_fixture.OtherPluginId}").And.Contain("Area: Admin").And.Contain("POST /reindex");
        line.Description.Should().NotContain("rebuild-everything");
    }
}
