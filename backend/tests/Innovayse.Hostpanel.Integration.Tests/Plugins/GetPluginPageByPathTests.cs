using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// One plugin page found by its path (<c>GET api/client/plugins/ui/{pluginId}/pages/by-path?path=</c> and the admin
/// twin), against Postgres and the test-only plugins of <see cref="PluginUiFixture"/>: a page found by path answers the
/// very body the lookup by id answers, and every refusal (an unknown or malformed path, a refused permission) answers
/// the body an unknown page gets.
/// </summary>
public sealed class GetPluginPageByPathTests : IClassFixture<PluginUiFixture>
{
    /// <summary>A well-formed plugin id that no plugin uses.</summary>
    private const string UnknownPluginId = "no-such-plugin";

    /// <summary>The members of an error body that must match for two refusals to be indistinguishable.</summary>
    private static readonly string[] ErrorMembers = ["type", "title", "status", "detail"];

    /// <summary>The host with the plugins.</summary>
    private readonly PluginUiFixture _fixture;

    /// <summary>Initializes the test.</summary>
    /// <param name="fixture">The host with the plugins.</param>
    public GetPluginPageByPathTests(PluginUiFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>The address of a client area page found by its path.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="path">The page path, escaped into the query string; <see langword="null"/> sends no path.</param>
    /// <returns>The relative URL.</returns>
    private static string ClientByPath(string pluginId, string? path)
    {
        return ByPath("client", pluginId, path);
    }

    /// <summary>The address of an admin area page found by its path.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="path">The page path, escaped into the query string.</param>
    /// <returns>The relative URL.</returns>
    private static string AdminByPath(string pluginId, string path)
    {
        return ByPath("admin", pluginId, path);
    }

    /// <summary>The address of a page found by its path in one area.</summary>
    /// <param name="area">The area segment: <c>client</c> or <c>admin</c>.</param>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="path">The page path, escaped into the query string; <see langword="null"/> sends no path.</param>
    /// <returns>The relative URL.</returns>
    private static string ByPath(string area, string pluginId, string? path)
    {
        var address = $"/api/{area}/plugins/ui/{pluginId}/pages/by-path";
        return path is null ? address : $"{address}?path={Uri.EscapeDataString(path)}";
    }

    /// <summary>The address of a client area page found by its id.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="pageId">The page id.</param>
    /// <returns>The relative URL.</returns>
    private static string ClientById(string pluginId, string pageId) => $"/api/client/plugins/ui/{pluginId}/pages/{pageId}";

    /// <summary>The address of an admin area page found by its id.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="pageId">The page id.</param>
    /// <returns>The relative URL.</returns>
    private static string AdminById(string pluginId, string pageId) => $"/api/admin/plugins/ui/{pluginId}/pages/{pageId}";

    /// <summary>Reads a response body as JSON.</summary>
    /// <param name="response">The response.</param>
    /// <returns>The document's root, detached from the response.</returns>
    private static async Task<JsonElement> JsonOfAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
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

    /// <summary>A client page found by its path answers exactly the body the lookup by id answers, in the request language.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_FoundByPath_AnswersTheBodyOfTheLookupById()
    {
        var (_, owner) = await _fixture.OwnerAsync("by-path-found");
        var http = PluginUiFixture.In(owner, "ru");

        using var byPath = await http.GetAsync(ClientByPath(_fixture.PluginId, $"/{PluginUiFixture.HomePage}"));
        using var byId = await http.GetAsync(ClientById(_fixture.PluginId, PluginUiFixture.HomePage));

        byPath.StatusCode.Should().Be(HttpStatusCode.OK);
        byId.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await JsonOfAsync(byPath);
        page.GetRawText().Should().Be((await JsonOfAsync(byId)).GetRawText());
        page.GetProperty("id").GetString().Should().Be(PluginUiFixture.HomePage);
        page.GetProperty("title").GetString().Should().Be("Главная страница");
        page.GetProperty("externalOrigins")[0].GetString().Should().Be(PluginUiFixture.ExternalOrigin);
    }

    /// <summary>The path's literal segments compare ignoring case, as a plugin route's do.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_PathInAnotherCase_FindsThePage()
    {
        var (_, http) = await _fixture.OwnerAsync("by-path-case");

        using var response = await http.GetAsync(ClientByPath(_fixture.PluginId, "/HOME"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonOfAsync(response)).GetProperty("id").GetString().Should().Be(PluginUiFixture.HomePage);
    }

    /// <summary>
    /// An unknown path, an unknown plugin, a disabled plugin and a missing path answer the same 404 body, so nothing about
    /// which of them it is can be probed.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_UnknownPathPluginDisabledAndMissingPath_AnswerTheUnknownPageBody()
    {
        var (_, http) = await _fixture.OwnerAsync("by-path-unknown");

        using var unknownById = await http.GetAsync(ClientById(UnknownPluginId, PluginUiFixture.HomePage));
        using var unknownPath = await http.GetAsync(ClientByPath(_fixture.PluginId, "/no-such-page"));
        using var unknownPlugin = await http.GetAsync(ClientByPath(UnknownPluginId, $"/{PluginUiFixture.HomePage}"));
        using var disabled = await http.GetAsync(ClientByPath(_fixture.DisabledPluginId, $"/{PluginUiFixture.HomePage}"));
        using var adminPage = await http.GetAsync(ClientByPath(_fixture.PluginId, $"/{PluginUiFixture.AdminPage}"));
        using var missing = await http.GetAsync(ClientByPath(_fixture.PluginId, null));

        var expected = await JsonOfAsync(unknownById);
        foreach (var response in new[] { unknownPath, unknownPlugin, disabled, adminPage, missing })
        {
            await AssertSameNotFoundAsync(response, expected);
        }
    }

    /// <summary>
    /// A path no page could have answers the unknown-page 404 body, never a 400, so a caller cannot learn which paths are
    /// well formed: no leading slash, an empty, <c>.</c> or <c>..</c> segment, a backslash, a control character, a
    /// trailing slash and a path over the length bound.
    /// </summary>
    /// <param name="path">The malformed path to send.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("home")]
    [InlineData("//home")]
    [InlineData("/home/")]
    [InlineData("/./home")]
    [InlineData("/../home")]
    [InlineData("/home/..")]
    [InlineData("/home\\x")]
    [InlineData("/home\n")]
    [InlineData("/home\u0000")]
    public async Task Client_MalformedPath_AnswersTheUnknownPageBody(string path)
    {
        var (_, http) = await _fixture.OwnerAsync($"by-path-malformed-{Guid.NewGuid():N}");

        using var unknown = await http.GetAsync(ClientById(UnknownPluginId, PluginUiFixture.HomePage));
        using var malformed = await http.GetAsync(ClientByPath(_fixture.PluginId, path));

        await AssertSameNotFoundAsync(malformed, await JsonOfAsync(unknown));
    }

    /// <summary>A path one character over <see cref="PluginPagePathMatcher.MaxPathLength"/> answers the unknown-page body.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_OverLongPath_AnswersTheUnknownPageBody()
    {
        var (_, http) = await _fixture.OwnerAsync("by-path-long");
        var path = "/" + new string('a', PluginPagePathMatcher.MaxPathLength);

        using var unknown = await http.GetAsync(ClientById(UnknownPluginId, PluginUiFixture.HomePage));
        using var overLong = await http.GetAsync(ClientByPath(_fixture.PluginId, path));

        await AssertSameNotFoundAsync(overLong, await JsonOfAsync(unknown));
    }

    /// <summary>A malformed plugin id answers the unknown-page body, as the lookup by id does.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_MalformedPluginId_AnswersTheUnknownPageBody()
    {
        var (_, http) = await _fixture.OwnerAsync("by-path-bad-plugin");

        using var unknown = await http.GetAsync(ClientById(UnknownPluginId, PluginUiFixture.HomePage));
        using var malformed = await http.GetAsync(ClientByPath("Upper-Case", $"/{PluginUiFixture.HomePage}"));

        await AssertSameNotFoundAsync(malformed, await JsonOfAsync(unknown));
    }

    /// <summary>A sub-user holding the page's permission opens it by path.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_SubUserWithThePermission_OpensThePageByPath()
    {
        var (owner, _) = await _fixture.OwnerAsync("by-path-sub-permitted");
        using var sub = await _fixture.SubUserAsync("by-path-sub-permitted-sub", owner.ClientId, ClientPermission.ViewPayInvoices);

        using var response = await sub.GetAsync(ClientByPath(_fixture.PluginId, $"/{PluginUiFixture.InvoicesPage}"));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonOfAsync(response)).GetProperty("id").GetString().Should().Be(PluginUiFixture.InvoicesPage);
    }

    /// <summary>
    /// A sub-user lacking the page's permission gets exactly the body an unknown page answers the same caller, and so does
    /// a page with no permission, which is the account holder's alone: the refusal does not reveal that the page exists.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_RefusedPermission_AnswersTheUnknownPageBody()
    {
        var (owner, _) = await _fixture.OwnerAsync("by-path-refused");
        using var sub = await _fixture.SubUserAsync("by-path-refused-sub", owner.ClientId, ClientPermission.ViewDomains);

        using var unknown = await sub.GetAsync(ClientByPath(_fixture.PluginId, "/no-such-page"));
        using var refused = await sub.GetAsync(ClientByPath(_fixture.PluginId, $"/{PluginUiFixture.InvoicesPage}"));
        using var holderOnly = await sub.GetAsync(ClientByPath(_fixture.PluginId, $"/{PluginUiFixture.AccountPage}"));

        var expected = await JsonOfAsync(unknown);
        await AssertSameNotFoundAsync(unknown, expected);
        await AssertSameNotFoundAsync(refused, expected);
        await AssertSameNotFoundAsync(holderOnly, expected);
    }

    /// <summary>An administrator opens an admin page by path and gets exactly the body the lookup by id answers.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Admin_FoundByPath_AnswersTheBodyOfTheLookupById()
    {
        using var admin = await _fixture.AdminClientAsync();

        using var byPath = await admin.GetAsync(AdminByPath(_fixture.PluginId, $"/{PluginUiFixture.AdminPage}"));
        using var byId = await admin.GetAsync(AdminById(_fixture.PluginId, PluginUiFixture.AdminPage));

        byPath.StatusCode.Should().Be(HttpStatusCode.OK);
        (await JsonOfAsync(byPath)).GetRawText().Should().Be((await JsonOfAsync(byId)).GetRawText());
    }

    /// <summary>The admin area answers an unknown path, a client page's path and a malformed path with the unknown-page body.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Admin_UnknownClientAndMalformedPath_AnswerTheUnknownPageBody()
    {
        using var admin = await _fixture.AdminClientAsync();

        using var unknown = await admin.GetAsync(AdminById(UnknownPluginId, PluginUiFixture.AdminPage));
        using var unknownPath = await admin.GetAsync(AdminByPath(_fixture.PluginId, "/no-such-page"));
        using var clientPage = await admin.GetAsync(AdminByPath(_fixture.PluginId, $"/{PluginUiFixture.HomePage}"));
        using var malformed = await admin.GetAsync(AdminByPath(_fixture.PluginId, "/../dashboard"));

        var expected = await JsonOfAsync(unknown);
        foreach (var response in new[] { unknownPath, clientPage, malformed })
        {
            await AssertSameNotFoundAsync(response, expected);
        }
    }

    /// <summary>A client token is refused at the admin lookup by path.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Admin_ClientToken_IsRefused()
    {
        var (_, http) = await _fixture.OwnerAsync("by-path-client-on-admin");

        using var response = await http.GetAsync(AdminByPath(_fixture.PluginId, $"/{PluginUiFixture.AdminPage}"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
