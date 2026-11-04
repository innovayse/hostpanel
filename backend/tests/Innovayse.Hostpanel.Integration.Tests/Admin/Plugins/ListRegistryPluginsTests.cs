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

            var page = await client.GetFromJsonAsync<JsonElement>("/api/admin/plugins/registry?pageSize=100");

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

            var page = await client.GetFromJsonAsync<JsonElement>("/api/admin/plugins/registry");

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
    [Fact]
    public async Task Filters_ReachTheRegistryAsync()
    {
        var registry = new FakePluginRegistryClient();
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.GetAsync("/api/admin/plugins/registry?q=stripe&category=Registrar&page=3&pageSize=5");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        registry.LastSearch.Should().Be(new RegistryPluginSearch("stripe", PluginRegistryCategory.Registrar, 3, 5));
        (await client.GetAsync("/api/admin/plugins/registry?q=%20%20")).StatusCode.Should().Be(HttpStatusCode.OK);
        registry.LastSearch!.Search.Should().BeNull();
    }

    /// <summary>A search over 100 characters and an undefined category are refused before the registry is asked.</summary>
    [Fact]
    public async Task InvalidFilters_AreRefusedAsync()
    {
        var registry = new FakePluginRegistryClient();
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        (await client.GetAsync($"/api/admin/plugins/registry?q={new string('a', 101)}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.GetAsync("/api/admin/plugins/registry?category=99")).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        registry.LastSearch.Should().BeNull();
    }

    /// <summary>An unreachable registry answers its own code and status, with the localised sentence.</summary>
    [Fact]
    public async Task AFailingRegistry_AnswersItsCodeAsync()
    {
        var registry = new FakePluginRegistryClient { FailureCode = HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE };
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.GetAsync("/api/admin/plugins/registry");

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REGISTRY_UNAVAILABLE);
    }

    /// <summary>With no registry configured (the default) the real client answers 409 <c>PLUGIN_REGISTRY_DISABLED</c> in the caller's language.</summary>
    [Fact]
    public async Task TheDefaultConfiguration_AnswersDisabledAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        client.DefaultRequestHeaders.Add("X-Language", "ru");

        var response = await client.GetAsync("/api/admin/plugins/registry");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REGISTRY_DISABLED);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Реестр плагинов");
    }

    /// <summary>A Client-role caller is refused and a caller with no credential is unauthorised.</summary>
    [Fact]
    public async Task OnlyAnAdministrator_MayBrowseAsync()
    {
        var clientRole = await PluginsTestSeeds.ClientRoleClientAsync(factory, "registry-list");
        (await clientRole.GetAsync("/api/admin/plugins/registry")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var anonymous = factory.CreateClient();
        (await anonymous.GetAsync("/api/admin/plugins/registry")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
