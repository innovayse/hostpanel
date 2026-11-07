using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>GET /api/admin/plugins/registry/updates</c>: only installed plugins with a newer registry version are listed, in the
/// browser's row shape; a staged upgrade is not offered again; registry failures keep their codes.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CheckPluginUpdatesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An outdated plugin is listed with both versions; a current one and one the registry does not know are not.</summary>
    [Fact]
    public async Task OnlyOutdatedPlugins_AreListedAsync()
    {
        var registry = new FakePluginRegistryClient();
        var outdated = $"test-old-{Guid.NewGuid():N}";
        var current = $"test-new-{Guid.NewGuid():N}";
        var unlisted = $"test-gone-{Guid.NewGuid():N}";
        registry.Publish(outdated, "1.5.0", [1]);
        registry.Publish(current, "1.0.0", [1]);
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        try
        {
            foreach (var id in new[] { outdated, current, unlisted })
            {
                (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(id))).StatusCode.Should().Be(HttpStatusCode.OK);
            }

            var updates = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/plugins/registry/updates");

            var ids = updates!.Select(u => u.GetProperty("id").GetString()).ToList();
            ids.Should().Contain(outdated).And.NotContain([current, unlisted]);
            var row = updates!.Single(u => u.GetProperty("id").GetString() == outdated);
            row.GetProperty("installed").GetBoolean().Should().BeTrue();
            row.GetProperty("installedVersion").GetString().Should().Be("1.0.0");
            row.GetProperty("latestVersion").GetString().Should().Be("1.5.0");
            row.GetProperty("updateAvailable").GetBoolean().Should().BeTrue();
            registry.LastLatestIds.Should().Contain([outdated, current, unlisted]);
        }
        finally
        {
            foreach (var id in new[] { outdated, current, unlisted })
            {
                await client.DeleteAsync($"/api/admin/plugins/{id}");
            }
        }
    }

    /// <summary>An upgrade that already waits for the restart at the registry's version is not offered again.</summary>
    [Fact]
    public async Task AnAlreadyStagedUpgrade_IsNotOfferedAgainAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-staged-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "2.0.0", [1]);
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        try
        {
            await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(pluginId));
            (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "2.0.0"))).StatusCode.Should().Be(HttpStatusCode.OK);

            var updates = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/plugins/registry/updates");

            updates!.Select(u => u.GetProperty("id").GetString()).Should().NotContain(pluginId);
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>The registry's failure keeps its code and status.</summary>
    [Fact]
    public async Task AFailingRegistry_AnswersItsCodeAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-fail-{Guid.NewGuid():N}";
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        try
        {
            await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(pluginId));
            registry.FailureCode = HostpanelErrorCodes.PLUGIN_REGISTRY_TIMED_OUT;

            var response = await client.GetAsync("/api/admin/plugins/registry/updates");

            response.StatusCode.Should().Be(HttpStatusCode.GatewayTimeout);
            await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REGISTRY_TIMED_OUT);
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>With the registry off by default the check answers 409 <c>PLUGIN_REGISTRY_DISABLED</c>.</summary>
    [Fact]
    public async Task TheDefaultConfiguration_AnswersDisabledAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var pluginId = $"test-off-{Guid.NewGuid():N}";

        try
        {
            await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(pluginId));

            var response = await client.GetAsync("/api/admin/plugins/registry/updates");

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REGISTRY_DISABLED);
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>A Client-role caller is refused and a caller with no credential is unauthorised.</summary>
    [Fact]
    public async Task OnlyAnAdministrator_MayCheckAsync()
    {
        var clientRole = await PluginsTestSeeds.ClientRoleClientAsync(factory, "registry-updates");
        (await clientRole.GetAsync("/api/admin/plugins/registry/updates")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var anonymous = factory.CreateClient();
        (await anonymous.GetAsync("/api/admin/plugins/registry/updates")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
