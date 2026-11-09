using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Admin.Plugins.Queries.ListPlugins;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>GET /api/admin/plugins</c>: an admin reads every plugin installed on disk with its load
/// status; a plugin the loader refused for a broken manifest rule is a refused row with its rule and is never loaded,
/// while a loaded plugin stays as it was; a refused upload can be removed; a caller with only the Client role is
/// refused, and the route is closed to a caller with no credential. This use case refuses nothing once authorized.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListPluginsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Installing then listing shows the plugin, not loaded.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InstallThenList_ShowsThePluginAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";

        try
        {
            var install = await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(pluginId));
            install.StatusCode.Should().Be(HttpStatusCode.OK);

            var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/plugins");
            var row = list!.Single(p => p.GetProperty("id").GetString() == pluginId);
            row.GetProperty("isLoaded").GetBoolean().Should()
                .BeFalse("installing does not load it -- only a restart does");
            row.GetProperty("isBuiltIn").GetBoolean().Should().BeFalse("it was uploaded");
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>The list shows the capabilities a manifest declares, as the SDK spells them, and an empty list for none (#818).</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APluginDeclaringCapabilities_ListsThemAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        var plainId = $"test-plugin-{Guid.NewGuid():N}";
        var zip = PluginsTestSeeds.BuildZip(new Dictionary<string, string>
        {
            ["plugin.json"] = PluginsTestSeeds.ManifestJson(pluginId, ",\"manifestVersion\":2,\"capabilities\":[\"endpoints\"],\"api\":{\"admin\":[{\"method\":\"get\",\"path\":\"/stats\"}]}", "Fulfilment"),
        });

        try
        {
            (await PluginsTestSeeds.InstallAsync(client, zip)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(plainId))).StatusCode.Should().Be(HttpStatusCode.OK);

            var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/plugins");

            var row = list!.Single(p => p.GetProperty("id").GetString() == pluginId);
            row.GetProperty("capabilities").EnumerateArray().Select(c => c.GetString()).Should().Equal("endpoints");
            list!.Single(p => p.GetProperty("id").GetString() == plainId).GetProperty("capabilities").GetArrayLength().Should().Be(0);
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
            await client.DeleteAsync($"/api/admin/plugins/{plainId}");
        }
    }

    /// <summary>A plugin in the build output's plugin folder is listed as built-in.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ABuiltInPlugin_IsMarkedBuiltInAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var pluginId = $"test-builtin-{Guid.NewGuid():N}";
        var builtIn = PluginsTestSeeds.StageBuiltInPlugin(pluginId);

        try
        {
            var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/plugins");

            var row = list!.Single(p => p.GetProperty("id").GetString() == pluginId);
            row.GetProperty("isBuiltIn").GetBoolean().Should().BeTrue();
        }
        finally
        {
            Directory.Delete(builtIn, recursive: true);
        }
    }

    /// <summary>
    /// A plugin the loader refused for a broken manifest rule is listed as refused, with its rule and detail, and never
    /// as loaded; a plugin that loaded stays a loaded row with no rule.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusedPlugin_IsListedAsRefusedAndALoadedOneStaysUnchangedAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var refusedId = $"test-refused-{Guid.NewGuid():N}";
        var loadedId = $"test-loaded-{Guid.NewGuid():N}";
        (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(refusedId))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(loadedId))).StatusCode.Should().Be(HttpStatusCode.OK);

        try
        {
            var registry = new FakePluginRegistry();
            registry.AddRefused(new RefusedPlugin(refusedId, PluginManifestRule.RoutePathInvalid, "routes[0].path: 'Bad'", false));
            registry.Add(PluginActivationTestSeeds.Plugin(loadedId, global::Hostpanel.Sdk.Plugins.Enums.PluginType.App, typeof(object)).Manifest);

            var rows = await NewHandler(registry).HandleAsync(new ListPluginsQuery(), CancellationToken.None);

            rows.IsSuccess.Should().BeTrue();
            var refused = rows.Value!.Single(r => r.Id == refusedId);
            refused.Status.Should().Be(PluginListStatus.Refused);
            refused.RefusedRule.Should().Be(PluginManifestRule.RoutePathInvalid);
            refused.RefusedDetail.Should().Contain("routes[0].path");
            refused.IsLoaded.Should().BeFalse("a refused plugin never reads as loaded");
            var loaded = rows.Value!.Single(r => r.Id == loadedId);
            loaded.Status.Should().Be(PluginListStatus.Loaded);
            loaded.RefusedRule.Should().BeNull();
            loaded.RefusedDetail.Should().BeNull();
            loaded.IsLoaded.Should().BeTrue();
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{refusedId}");
            await client.DeleteAsync($"/api/admin/plugins/{loadedId}");
        }
    }

    /// <summary>A loaded plugin that ships an icon carries the icon endpoint as <c>iconUrl</c>; one without, and a refused row, carry null.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IconUrl_IsSetOnlyForALoadedPluginWithAnIconAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var withIconId = $"test-icon-{Guid.NewGuid():N}";
        var plainId = $"test-plain-{Guid.NewGuid():N}";
        (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(withIconId))).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(plainId))).StatusCode.Should().Be(HttpStatusCode.OK);

        try
        {
            var registry = new FakePluginRegistry();
            registry.Add(PluginActivationTestSeeds.Plugin(withIconId, global::Hostpanel.Sdk.Plugins.Enums.PluginType.App, typeof(object)).Manifest);
            registry.Add(PluginActivationTestSeeds.Plugin(plainId, global::Hostpanel.Sdk.Plugins.Enums.PluginType.App, typeof(object)).Manifest);
            registry.AddIcon(withIconId, new PluginIconDto("image/png", [1, 2, 3], "\"t\""));

            var rows = await NewHandler(registry).HandleAsync(new ListPluginsQuery(), CancellationToken.None);

            rows.Value!.Single(r => r.Id == withIconId).IconUrl.Should().Be($"/api/admin/plugins/{withIconId}/icon");
            rows.Value!.Single(r => r.Id == plainId).IconUrl.Should().BeNull();
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{withIconId}");
            await client.DeleteAsync($"/api/admin/plugins/{plainId}");
        }
    }

    /// <summary>A refused upload is removed like any other: the folder goes and the row with it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusedUpload_CanBeRemovedAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var pluginId = $"test-refused-{Guid.NewGuid():N}";
        (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(pluginId))).StatusCode.Should().Be(HttpStatusCode.OK);
        var registry = new FakePluginRegistry();
        registry.AddRefused(new RefusedPlugin(pluginId, PluginManifestRule.LabelKeyMissing, "menu.reports", false));
        (await NewHandler(registry).HandleAsync(new ListPluginsQuery(), CancellationToken.None))
            .Value!.Should().Contain(r => r.Id == pluginId && r.Status == PluginListStatus.Refused);

        var remove = await client.DeleteAsync($"/api/admin/plugins/{pluginId}");

        remove.StatusCode.Should().Be(HttpStatusCode.OK);
        Directory.Exists(PluginsTestSeeds.InstalledFolder(factory, pluginId)).Should().BeFalse();
        (await NewHandler(registry).HandleAsync(new ListPluginsQuery(), CancellationToken.None))
            .Value!.Should().NotContain(r => r.Id == pluginId, "the refusal belongs to the startup snapshot; with the folder gone there is no row");
    }

    /// <summary>Builds the list handler from the host's own store and data context over a fake registry.</summary>
    /// <param name="registry">The registry that reports which plugins loaded and which were refused.</param>
    /// <returns>The handler.</returns>
    private ListPluginsHandler NewHandler(IPluginRegistry registry)
    {
        var scope = factory.Services.CreateScope();
        return new ListPluginsHandler(
            scope.ServiceProvider.GetRequiredService<IPluginPackageStore>(),
            registry,
            scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>());
    }

    /// <summary>A caller with only the Client role is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToTheClientRoleAsync()
    {
        var client = await PluginsTestSeeds.ClientRoleClientAsync(factory, "list-plugins-forbidden");

        var response = await client.GetAsync("/api/admin/plugins");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The admin routes are not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IsClosedToAnonymousCallersAsync()
    {
        var response = await factory.CreateClient().GetAsync("/api/admin/plugins");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
