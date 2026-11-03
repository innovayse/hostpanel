using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Clients.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// The plugin ui index of both areas (<c>GET api/client/plugins/ui</c> and <c>GET api/admin/plugins/ui</c>), against
/// Postgres and the test-only plugins of <see cref="PluginUiFixture"/>: only enabled app plugins are listed, pages and
/// menus follow the caller's permissions, texts follow <c>X-Language</c>, and menus come in a fixed order.
/// </summary>
public sealed class GetPluginUiTests : IClassFixture<PluginUiFixture>, IClassFixture<EmptyPluginUiFixture>
{
    /// <summary>The client area index.</summary>
    private const string ClientUrl = "/api/client/plugins/ui";

    /// <summary>The admin area index.</summary>
    private const string AdminUrl = "/api/admin/plugins/ui";

    /// <summary>The host with the plugins.</summary>
    private readonly PluginUiFixture _fixture;

    /// <summary>The host with none.</summary>
    private readonly EmptyPluginUiFixture _empty;

    /// <summary>Initializes the test.</summary>
    /// <param name="fixture">The host with the plugins.</param>
    /// <param name="empty">The host with an empty registry.</param>
    public GetPluginUiTests(PluginUiFixture fixture, EmptyPluginUiFixture empty)
    {
        _fixture = fixture;
        _empty = empty;
    }

    /// <summary>Reads the index a request answered.</summary>
    /// <param name="http">The caller.</param>
    /// <param name="url">The index address.</param>
    /// <returns>The root of the answered document, detached from the response.</returns>
    private static async Task<JsonElement> IndexAsync(HttpClient http, string url)
    {
        var response = await http.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    /// <summary>The ids of the rows of one list as <c>pluginId/id</c>, in the order answered.</summary>
    /// <param name="index">The answered index.</param>
    /// <param name="list">The list: <c>menus</c> or <c>pages</c>.</param>
    /// <returns>The identifiers.</returns>
    private static List<string> KeysOf(JsonElement index, string list)
    {
        return index.GetProperty(list).EnumerateArray()
            .Select(row => $"{row.GetProperty("pluginId").GetString()}/{row.GetProperty("id").GetString()}")
            .ToList();
    }

    /// <summary>The value of one member of the row with a key.</summary>
    /// <param name="index">The answered index.</param>
    /// <param name="list">The list: <c>menus</c> or <c>pages</c>.</param>
    /// <param name="key">The <c>pluginId/id</c> of the row.</param>
    /// <param name="member">The member to read.</param>
    /// <returns>The member's text.</returns>
    private static string? ValueOf(JsonElement index, string list, string key, string member)
    {
        var row = index.GetProperty(list).EnumerateArray()
            .Single(candidate => $"{candidate.GetProperty("pluginId").GetString()}/{candidate.GetProperty("id").GetString()}" == key);
        return row.GetProperty(member).GetString();
    }

    /// <summary>The account holder sees every client page of the enabled app plugins, grouped by plugin id and in manifest order.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_OwnerSeesEveryPageOfEnabledAppPlugins_InOrder()
    {
        var (_, http) = await _fixture.OwnerAsync("ui-owner-pages");

        var index = await IndexAsync(http, ClientUrl);

        KeysOf(index, "pages").Should().Equal(
            $"{_fixture.PluginId}/{PluginUiFixture.HomePage}",
            $"{_fixture.PluginId}/{PluginUiFixture.InvoicesPage}",
            $"{_fixture.PluginId}/{PluginUiFixture.AccountPage}",
            $"{_fixture.SecondPluginId}/{PluginUiFixture.StartPage}");
    }

    /// <summary>
    /// Pages are grouped by plugin id in ordinal order although the plugin whose id sorts last was registered first, and
    /// keep the manifest's order within a plugin.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_PagesAreOrderedByPluginId_NotByRegistration()
    {
        var (_, http) = await _fixture.OwnerAsync("ui-owner-plugin-order");

        var index = await IndexAsync(http, ClientUrl);

        string.CompareOrdinal(_fixture.PluginId, _fixture.SecondPluginId).Should().BeNegative();
        var pluginIds = index.GetProperty("pages").EnumerateArray()
            .Select(row => row.GetProperty("pluginId").GetString())
            .ToList();
        pluginIds.Should().Equal(_fixture.PluginId, _fixture.PluginId, _fixture.PluginId, _fixture.SecondPluginId);
        KeysOf(index, "pages").Take(3).Should().Equal(
            $"{_fixture.PluginId}/{PluginUiFixture.HomePage}",
            $"{_fixture.PluginId}/{PluginUiFixture.InvoicesPage}",
            $"{_fixture.PluginId}/{PluginUiFixture.AccountPage}");
    }

    /// <summary>The page path and plugin id are answered as declared.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_PagesCarryTheirDeclaredPath()
    {
        var (_, http) = await _fixture.OwnerAsync("ui-owner-path");

        var index = await IndexAsync(http, ClientUrl);

        ValueOf(index, "pages", $"{_fixture.PluginId}/{PluginUiFixture.InvoicesPage}", "path").Should().Be("/invoices");
    }

    /// <summary>Menus are ordered by order, then plugin id, then menu id, and every menu opens a listed page.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_MenusAreOrderedByOrderThenPluginThenId()
    {
        var (_, http) = await _fixture.OwnerAsync("ui-owner-menus");

        var index = await IndexAsync(http, ClientUrl);

        KeysOf(index, "menus").Should().Equal(
            $"{_fixture.SecondPluginId}/second-first",
            $"{_fixture.PluginId}/invoices",
            $"{_fixture.PluginId}/home",
            $"{_fixture.SecondPluginId}/second-tie",
            $"{_fixture.PluginId}/account",
            $"{_fixture.PluginId}/invoices-shortcut");
    }

    /// <summary>A sub-user holding <see cref="ClientPermission.ViewPayInvoices"/> sees the open and the invoice pages, not the account holder's, and their menus follow.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_SubUserWithViewPayInvoices_SeesAnyAndInvoicePagesOnly()
    {
        var (owner, _) = await _fixture.OwnerAsync("ui-sub-invoices");
        var sub = await _fixture.SubUserAsync("ui-sub-invoices-sub", owner.ClientId, ClientPermission.ViewPayInvoices);

        var index = await IndexAsync(sub, ClientUrl);

        KeysOf(index, "pages").Should().Equal(
            $"{_fixture.PluginId}/{PluginUiFixture.HomePage}",
            $"{_fixture.PluginId}/{PluginUiFixture.InvoicesPage}",
            $"{_fixture.SecondPluginId}/{PluginUiFixture.StartPage}");
        KeysOf(index, "menus").Should().Equal(
            $"{_fixture.SecondPluginId}/second-first",
            $"{_fixture.PluginId}/invoices",
            $"{_fixture.PluginId}/home",
            $"{_fixture.SecondPluginId}/second-tie",
            $"{_fixture.PluginId}/invoices-shortcut");
    }

    /// <summary>A sub-user with no flags sees only the pages open to everyone, and no menu to a page they cannot open.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_SubUserWithNoFlags_SeesOnlyAnyPagesAndTheirMenus()
    {
        var (owner, _) = await _fixture.OwnerAsync("ui-sub-none");
        var sub = await _fixture.SubUserAsync("ui-sub-none-sub", owner.ClientId, ClientPermission.None);

        var index = await IndexAsync(sub, ClientUrl);

        KeysOf(index, "pages").Should().Equal(
            $"{_fixture.PluginId}/{PluginUiFixture.HomePage}",
            $"{_fixture.SecondPluginId}/{PluginUiFixture.StartPage}");
        KeysOf(index, "menus").Should().Equal(
            $"{_fixture.SecondPluginId}/second-first",
            $"{_fixture.PluginId}/home",
            $"{_fixture.SecondPluginId}/second-tie");
    }

    /// <summary>A disabled plugin and a plugin of another type are absent from the client index, though they declare pages.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_DisabledAndNonAppPlugins_AreAbsent()
    {
        var (_, http) = await _fixture.OwnerAsync("ui-owner-absent");

        var index = await IndexAsync(http, ClientUrl);

        foreach (var list in new[] { "pages", "menus" })
        {
            KeysOf(index, list).Should().NotContain(key => key.StartsWith(_fixture.DisabledPluginId) || key.StartsWith(_fixture.NotAppPluginId));
        }
    }

    /// <summary>The admin index lists admin pages and menus only, of the enabled app plugins.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Admin_ListsAdminPagesOnly()
    {
        using var admin = await _fixture.AdminClientAsync();

        var index = await IndexAsync(admin, AdminUrl);

        KeysOf(index, "pages").Should().Equal($"{_fixture.PluginId}/{PluginUiFixture.AdminPage}");
        KeysOf(index, "menus").Should().Equal($"{_fixture.PluginId}/admin");
    }

    /// <summary>A client token is refused at the admin index.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Admin_ClientToken_IsRefused()
    {
        var (_, http) = await _fixture.OwnerAsync("ui-owner-admin-index");

        var response = await http.GetAsync(AdminUrl);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Titles and labels are answered in the language asked for, with plain text passing through.</summary>
    /// <param name="language">The <c>X-Language</c> to send.</param>
    /// <param name="home">The expected label of the home menu.</param>
    /// <param name="title">The expected title of the home page.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("en", "Home", "Home page")]
    [InlineData("ru", "Главная", "Главная страница")]
    [InlineData("hy", "Գլխավոր", "Գլխավոր էջ")]
    public async Task Client_TextsFollowTheRequestLanguage_AndPlainTextPassesThrough(string language, string home, string title)
    {
        var (_, owner) = await _fixture.OwnerAsync($"ui-lang-{language}");
        var http = PluginUiFixture.In(owner, language);

        var index = await IndexAsync(http, ClientUrl);

        var homeKey = $"{_fixture.PluginId}/home";
        ValueOf(index, "menus", homeKey, "label").Should().Be(home);
        ValueOf(index, "pages", $"{_fixture.PluginId}/{PluginUiFixture.HomePage}", "title").Should().Be(title);
        ValueOf(index, "menus", $"{_fixture.PluginId}/account", "label").Should().Be("Account");
        ValueOf(index, "pages", $"{_fixture.PluginId}/{PluginUiFixture.AccountPage}", "title").Should().Be("Account overview");
    }

    /// <summary>A plugin that ships no texts for the language, or none at all, has its labels answered as written.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_APluginWithoutTexts_AnswersItsLabelsAsWritten()
    {
        var (_, owner) = await _fixture.OwnerAsync("ui-lang-plain");
        var http = PluginUiFixture.In(owner, "ru");

        var index = await IndexAsync(http, ClientUrl);

        ValueOf(index, "menus", $"{_fixture.SecondPluginId}/second-first", "label").Should().Be("Second first");
    }

    /// <summary>The admin index follows the request language as well.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Admin_TextsFollowTheRequestLanguage()
    {
        using var admin = await _fixture.AdminClientAsync();
        PluginUiFixture.In(admin, "ru");

        var index = await IndexAsync(admin, AdminUrl);

        ValueOf(index, "menus", $"{_fixture.PluginId}/admin", "label").Should().Be("Администрирование");
        ValueOf(index, "pages", $"{_fixture.PluginId}/{PluginUiFixture.AdminPage}", "title").Should().Be("Страница администратора");
    }

    /// <summary>With no plugin loaded the client index is two empty lists.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_WithNoPlugins_ListsAreEmpty()
    {
        var owner = await ClientsTestSeeds.RegisterClientAsync(_empty.Factory, "ui-empty-client");
        using var http = AppPluginFixture.WithBearerOf(_empty.Host, owner.Client);

        var index = await IndexAsync(http, ClientUrl);

        index.GetProperty("menus").GetArrayLength().Should().Be(0);
        index.GetProperty("pages").GetArrayLength().Should().Be(0);
    }

    /// <summary>With no plugin loaded the admin index is two empty lists.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Admin_WithNoPlugins_ListsAreEmpty()
    {
        using var admin = await PluginUiFixture.AdminClientOfAsync(_empty.Host, _empty.Factory);

        var index = await IndexAsync(admin, AdminUrl);

        index.GetProperty("menus").GetArrayLength().Should().Be(0);
        index.GetProperty("pages").GetArrayLength().Should().Be(0);
    }
}
