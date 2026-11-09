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
    /// <returns>A task that completes when the test has run.</returns>
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
    /// <returns>A task that completes when the test has run.</returns>
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
    /// <returns>A task that completes when the test has run.</returns>
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
    /// <returns>A task that completes when the test has run.</returns>
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
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OnlyAnAdministrator_MayCheckAsync()
    {
        var clientRole = await PluginsTestSeeds.ClientRoleClientAsync(factory, "registry-updates");
        (await clientRole.GetAsync("/api/admin/plugins/registry/updates")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var anonymous = factory.CreateClient();
        (await anonymous.GetAsync("/api/admin/plugins/registry/updates")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A plugin with a stored licence key is read again with it: the licensee's view of the newest version wins over the
    /// anonymous one, and the key is sent. A plugin without a key is asked about without one.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AStoredLicenceKey_IsSentAndItsViewWinsAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-lic-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.5.0", [1]);
        registry.Details[pluginId] = new Application.Admin.Plugins.Common.RegistryPluginDetails(
            registry.Details[pluginId].Summary with { LatestVersion = "3.0.0" }, null, null, null, null, null, null, null);
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        try
        {
            await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(pluginId));

            var anonymous = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/plugins/registry/updates");
            anonymous!.Single(u => u.GetProperty("id").GetString() == pluginId).GetProperty("latestVersion").GetString().Should().Be("1.5.0");
            registry.LastLicenceKey.Should().BeNull();

            (await client.PutAsJsonAsync($"/api/admin/plugins/registry/{pluginId}/licence", new { key = "LICENCE-KEY-update" })).StatusCode.Should().Be(HttpStatusCode.NoContent);

            var licensed = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/plugins/registry/updates");

            licensed!.Single(u => u.GetProperty("id").GetString() == pluginId).GetProperty("latestVersion").GetString().Should().Be("3.0.0");
            registry.LastLicenceKey.Should().Be("LICENCE-KEY-update");
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/registry/{pluginId}/licence");
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>A plugin the registry does not show to the licensee keeps the answer it had and does not fail the check.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APluginTheRegistryHidesFromTheLicensee_KeepsItsAnswerAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-lic-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.5.0", [1]);
        registry.Details.Remove(pluginId);
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        try
        {
            await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(pluginId));
            await client.PutAsJsonAsync($"/api/admin/plugins/registry/{pluginId}/licence", new { key = "LICENCE-KEY-hidden" });

            var response = await client.GetAsync("/api/admin/plugins/registry/updates");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var rows = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
            rows!.Single(u => u.GetProperty("id").GetString() == pluginId).GetProperty("latestVersion").GetString().Should().Be("1.5.0");
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/registry/{pluginId}/licence");
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>With two plugins that have keys, each plugin page call carries only that plugin's own key; a plugin without a key is asked without one.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TwoPlugins_EachSendOnlyTheirOwnKeyAsync()
    {
        var registry = new FakePluginRegistryClient();
        var first = $"test-lic-a{Guid.NewGuid():N}";
        var second = $"test-lic-b{Guid.NewGuid():N}";
        var third = $"test-lic-c{Guid.NewGuid():N}";
        foreach (var id in new[] { first, second, third })
        {
            registry.Publish(id, "1.5.0", [1]);
        }

        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        try
        {
            foreach (var id in new[] { first, second, third })
            {
                await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(id));
            }

            await client.PutAsJsonAsync($"/api/admin/plugins/registry/{first}/licence", new { key = "KEY-FIRST" });
            await client.PutAsJsonAsync($"/api/admin/plugins/registry/{second}/licence", new { key = "KEY-SECOND" });

            (await client.GetAsync("/api/admin/plugins/registry/updates")).StatusCode.Should().Be(HttpStatusCode.OK);

            registry.PluginPageKeys[first].Should().Be("KEY-FIRST");
            registry.PluginPageKeys[second].Should().Be("KEY-SECOND");
            registry.PluginPageKeys.ContainsKey(third).Should().BeFalse("a plugin without a key is not asked again");
        }
        finally
        {
            foreach (var id in new[] { first, second, third })
            {
                await client.DeleteAsync($"/api/admin/plugins/registry/{id}/licence");
                await client.DeleteAsync($"/api/admin/plugins/{id}");
            }
        }
    }
}
