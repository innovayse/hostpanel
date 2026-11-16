using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>GET /api/admin/plugins/registry</c>: a page of the registry with each plugin marked installed, at which version and
/// whether an update is published; the filters reach the registry; a switched-off registry answers
/// <c>PLUGIN_REGISTRY_DISABLED</c> in the caller's language; the route is admin only.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListRegistryPluginsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The page carries the contract members, marks the installed plugin and offers its update.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InstalledPlugin_IsMarkedWithItsVersionAndTheUpdateAsync()
    {
        var registry = new FakePluginRegistryClient();
        var installedId = $"test-listed-{Guid.NewGuid():N}";
        var freshId = $"test-fresh-{Guid.NewGuid():N}";
        registry.Publish(installedId, "2.0.0", [1], category: PluginRegistryCategory.Payment);
        registry.Publish(freshId, "1.0.0", [1]);
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        try
        {
            (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(installedId))).StatusCode.Should().Be(HttpStatusCode.OK);

            var page = await client.GetFromJsonAsync<JsonElement>("/api/admin/plugins/registry?page=1&pageSize=100");

            page.GetProperty("page").GetInt32().Should().Be(1);
            page.GetProperty("pageSize").GetInt32().Should().Be(100);
            page.GetProperty("total").GetInt32().Should().Be(2);
            var items = page.GetProperty("items").EnumerateArray().ToDictionary(i => i.GetProperty("id").GetString()!);
            var installed = items[installedId];
            installed.GetProperty("installed").GetBoolean().Should().BeTrue();
            installed.GetProperty("installedVersion").GetString().Should().Be("1.0.0");
            installed.GetProperty("latestVersion").GetString().Should().Be("2.0.0");
            installed.GetProperty("updateAvailable").GetBoolean().Should().BeTrue();
            installed.GetProperty("category").GetString().Should().Be("Payment");
            installed.GetProperty("pricing").GetString().Should().Be("Free");
            installed.GetProperty("verified").GetBoolean().Should().BeTrue();
            installed.TryGetProperty("sdkVersion", out _).Should().BeTrue();
            installed.TryGetProperty("iconUrl", out _).Should().BeTrue();
            var fresh = items[freshId];
            fresh.GetProperty("installed").GetBoolean().Should().BeFalse();
            fresh.GetProperty("installedVersion").ValueKind.Should().Be(JsonValueKind.Null);
            fresh.GetProperty("updateAvailable").GetBoolean().Should().BeFalse();
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{installedId}");
        }
    }

    /// <summary>A plugin installed at the registry's newest version is not offered an update.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InstalledAtTheLatestVersion_OffersNoUpdateAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-current-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", [1]);
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        try
        {
            await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(pluginId));

            var page = await client.GetFromJsonAsync<JsonElement>("/api/admin/plugins/registry?page=1&pageSize=20");

            var item = page.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetString() == pluginId);
            item.GetProperty("installed").GetBoolean().Should().BeTrue();
            item.GetProperty("updateAvailable").GetBoolean().Should().BeFalse();
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>The search text, the category and the page reach the registry; a blank search is no search.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Filters_ReachTheRegistryAsync()
    {
        var registry = new FakePluginRegistryClient();
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.GetAsync("/api/admin/plugins/registry?search=stripe&category=Registrar&page=3&pageSize=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        registry.LastSearch.Should().Be(new RegistryPluginSearch("stripe", PluginRegistryCategory.Registrar, 3, 5));
        (await client.GetAsync("/api/admin/plugins/registry?page=1&pageSize=20&search=%20%20")).StatusCode.Should().Be(HttpStatusCode.OK);
        registry.LastSearch!.Search.Should().BeNull();
    }

    /// <summary>
    /// A missing page or page size, a page below 1, a page size outside 1 to 100 and a category that is not a member are refused before the registry is
    /// asked; an unknown category name or number is refused by the model binder, so the field is named in the answer.
    /// </summary>
    /// <param name="query">The query string that must be refused.</param>
    /// <param name="field">The field the 400 names.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("pageSize=20", "page")]
    [InlineData("page=1", "pageSize")]
    [InlineData("page=0&pageSize=20", "page")]
    [InlineData("page=1&pageSize=0", "pageSize")]
    [InlineData("page=1&pageSize=101", "pageSize")]
    [InlineData("page=1&pageSize=20&category=Unknown", "category")]
    [InlineData("page=1&pageSize=20&category=99", "category")]
    public async Task InvalidQuery_IsRefusedAsync(string query, string field)
    {
        var registry = new FakePluginRegistryClient();
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.GetAsync($"/api/admin/plugins/registry?{query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain(field, because: "the 400 names the offending field");
        registry.LastSearch.Should().BeNull();
    }

    /// <summary>A search over 100 characters is refused before the registry is asked.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ASearchOver100Characters_IsRefusedAsync()
    {
        var registry = new FakePluginRegistryClient();
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.GetAsync($"/api/admin/plugins/registry?page=1&pageSize=20&search={new string('a', 101)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        registry.LastSearch.Should().BeNull();
    }

    /// <summary>An unreachable registry answers its own code and status, with the localised sentence.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AFailingRegistry_AnswersItsCodeAsync()
    {
        var registry = new FakePluginRegistryClient { FailureCode = HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE };
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.GetAsync("/api/admin/plugins/registry?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE);
    }

    /// <summary>With no registry configured (the default) the real client answers 409 <c>PLUGIN_REGISTRY_DISABLED</c> in the caller's language.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheDefaultConfiguration_AnswersDisabledAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        client.DefaultRequestHeaders.Add("X-Language", "ru");

        var response = await client.GetAsync("/api/admin/plugins/registry?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REGISTRY_DISABLED);
        // The body is JSON, which escapes non-ASCII text, so the sentence is read from the parsed detail, not the raw string.
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("detail").GetString().Should().Contain("Реестр плагинов");
    }

    /// <summary>A Client-role caller is refused and a caller with no credential is unauthorised.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OnlyAnAdministrator_MayBrowseAsync()
    {
        var clientRole = await PluginsTestSeeds.ClientRoleClientAsync(factory, "registry-list");
        (await clientRole.GetAsync("/api/admin/plugins/registry")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var anonymous = factory.CreateClient();
        (await anonymous.GetAsync("/api/admin/plugins/registry")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// An installed plugin whose newest version needs a newer host is not marked as having an update, and one with an older
    /// newer version that runs here is offered that version, which is the one an update installs.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnUpdateThatDoesNotRunOnThisHost_IsNotOfferedAsync()
    {
        var registry = new FakePluginRegistryClient();
        var tooNew = $"test-toonew-{Guid.NewGuid():N}";
        var fitting = $"test-fits-{Guid.NewGuid():N}";
        registry.Publish(tooNew, "2.0.0", [1], minHostVersion: "5.0.0");
        registry.Publish(fitting, "1.5.0", [1]);
        registry.Publish(fitting, "2.0.0", [1], minHostVersion: "5.0.0");
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry, hostVersion: "1.4.0");

        try
        {
            foreach (var id in new[] { tooNew, fitting })
            {
                (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(id))).StatusCode.Should().Be(HttpStatusCode.OK);
            }

            var page = await client.GetFromJsonAsync<JsonElement>("/api/admin/plugins/registry?page=1&pageSize=100");

            var items = page.GetProperty("items").EnumerateArray().ToDictionary(i => i.GetProperty("id").GetString()!);
            items[tooNew].GetProperty("updateAvailable").GetBoolean().Should().BeFalse();
            items[tooNew].GetProperty("latestVersion").GetString().Should().Be("2.0.0");
            items[fitting].GetProperty("updateAvailable").GetBoolean().Should().BeTrue();
            items[fitting].GetProperty("latestVersion").GetString().Should().Be("1.5.0");
        }
        finally
        {
            foreach (var id in new[] { tooNew, fitting })
            {
                await client.DeleteAsync($"/api/admin/plugins/{id}");
            }
        }
    }
}
