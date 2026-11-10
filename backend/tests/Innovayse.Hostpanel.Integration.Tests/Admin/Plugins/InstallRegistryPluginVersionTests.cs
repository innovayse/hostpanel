using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>POST /api/admin/plugins/registry/{id}/install</c>, the chosen version: the stored licence key is sent with every call
/// of the install; a yanked, rejected or not yet published version, and a version whose host range excludes the running
/// host, are refused with their codes and audited before anything is downloaded; a version the registry serves but does
/// not describe is not installed; a version older than the installed one needs <c>allowDowngrade</c>, which defaults to
/// false; a deprecated version still installs.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class InstallRegistryPluginVersionTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route of a registry install.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The route.</returns>
    private static string Route(string pluginId) => $"/api/admin/plugins/registry/{pluginId}/install";

    /// <summary>The route of the licence key.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The route.</returns>
    private static string LicenceRoute(string pluginId) => $"/api/admin/plugins/registry/{pluginId}/licence";

    /// <summary>Tells whether the activity log holds a refusal line of the registry for a plugin.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="reason">A phrase the line must contain.</param>
    /// <returns><see langword="true"/> when such a line exists.</returns>
    private async Task<bool> RefusalAuditedAsync(string pluginId, string reason)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ActivityLogs.AsNoTracking().AnyAsync(l =>
            l.Description.StartsWith("Refused Plugin From Registry") && l.Description.Contains(pluginId) && l.Description.Contains(reason));
    }

    /// <summary>The stored licence key is sent to the registry on the install, and none is sent when none is stored.</summary>
    /// <param name="stored">Whether a key is stored for the plugin.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheStoredLicenceKey_IsSentWithTheInstallAsync(bool stored)
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-reg-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", PluginSigningTestSeeds.SignedZip(pluginId), pricing: PluginRegistryPricing.Paid);
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        try
        {
            if (stored)
            {
                await client.PutAsJsonAsync(LicenceRoute(pluginId), new { key = "LICENCE-KEY-install" });
            }

            var response = await client.PostAsJsonAsync(Route(pluginId), PluginRegistryTestSeeds.InstallBody("1.0.0"));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            registry.LastLicenceKey.Should().Be(stored ? "LICENCE-KEY-install" : null);
            (await response.Content.ReadAsStringAsync()).Should().NotContain("LICENCE-KEY-install");
        }
        finally
        {
            await client.DeleteAsync(LicenceRoute(pluginId));
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>A registry that wants a licence key, or refuses the one sent, answers 422 and nothing is downloaded or installed.</summary>
    /// <param name="code">The registry's failure.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("PLUGIN_REGISTRY_LICENCE_REQUIRED")]
    [InlineData("PLUGIN_REGISTRY_LICENCE_INVALID")]
    public async Task ALicenceRefusal_AnswersUnprocessableAsync(string code)
    {
        var registry = new FakePluginRegistryClient { FailureCode = code };
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.PostAsJsonAsync(Route("some-plugin"), PluginRegistryTestSeeds.InstallBody("1.0.0"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        await PluginsTestSeeds.AssertCodeAsync(response, code);
        registry.DownloadCount.Should().Be(0);
    }

    /// <summary>A yanked version is refused with 409 before it is downloaded, and the refusal is audited.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AYankedVersion_IsRefusedAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-reg-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", PluginSigningTestSeeds.SignedZip(pluginId), status: PluginRegistryVersionStatus.Yanked);
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.PostAsJsonAsync(Route(pluginId), PluginRegistryTestSeeds.InstallBody("1.0.0"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REGISTRY_VERSION_YANKED);
        registry.DownloadCount.Should().Be(0);
        Directory.Exists(PluginsTestSeeds.InstalledFolder(factory, pluginId)).Should().BeFalse();
        (await RefusalAuditedAsync(pluginId, "Yanked")).Should().BeTrue();
    }

    /// <summary>A version listed twice, published and then yanked, is judged by the yanked row and is never downloaded.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ADuplicateVersion_IsJudgedByItsStrictestStatusAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-reg-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", PluginSigningTestSeeds.SignedZip(pluginId));
        var published = registry.Versions[pluginId].Single();
        registry.Versions[pluginId] = [published, published with { Status = PluginRegistryVersionStatus.Yanked }];
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.PostAsJsonAsync(Route(pluginId), PluginRegistryTestSeeds.InstallBody("1.0.0"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REGISTRY_VERSION_YANKED);
        registry.DownloadCount.Should().Be(0);
    }

    /// <summary>A version the registry rejected is refused with 409 before it is downloaded, and the refusal is audited.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ARejectedVersion_IsRefusedAsCompromisedAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-reg-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", PluginSigningTestSeeds.SignedZip(pluginId), status: PluginRegistryVersionStatus.Rejected);
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.PostAsJsonAsync(Route(pluginId), PluginRegistryTestSeeds.InstallBody("1.0.0"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REGISTRY_VERSION_COMPROMISED);
        registry.DownloadCount.Should().Be(0);
        (await RefusalAuditedAsync(pluginId, "Rejected")).Should().BeTrue();
    }

    /// <summary>A version still in review is not published yet, so it answers 404.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AVersionInReview_AnswersNotFoundAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-reg-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", PluginSigningTestSeeds.SignedZip(pluginId), status: PluginRegistryVersionStatus.InReview);
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.PostAsJsonAsync(Route(pluginId), PluginRegistryTestSeeds.InstallBody("1.0.0"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REGISTRY_PLUGIN_NOT_FOUND);
        registry.DownloadCount.Should().Be(0);
    }

    /// <summary>A deprecated version still installs: the publisher only advises against it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ADeprecatedVersion_StillInstallsAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-reg-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", PluginSigningTestSeeds.SignedZip(pluginId), status: PluginRegistryVersionStatus.Deprecated);
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        try
        {
            var response = await client.PostAsJsonAsync(Route(pluginId), PluginRegistryTestSeeds.InstallBody("1.0.0"));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>A version the registry serves but does not describe is not installed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AVersionTheRegistryDoesNotDescribe_IsNotInstalledAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-reg-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", PluginSigningTestSeeds.SignedZip(pluginId));
        registry.Versions[pluginId] = [];
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.PostAsJsonAsync(Route(pluginId), PluginRegistryTestSeeds.InstallBody("1.0.0"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REGISTRY_PLUGIN_NOT_FOUND);
        registry.DownloadCount.Should().Be(0);
    }

    /// <summary>A version whose host range excludes the running host is refused with 409 before it is downloaded, and audited.</summary>
    /// <param name="hostVersion">The version the host reports.</param>
    /// <param name="min">The version's lower bound.</param>
    /// <param name="max">The version's upper bound.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("1.4.0", "2.0.0", null)]
    [InlineData("1.4.0", null, "1.3.0")]
    [InlineData("1.4.0", "garbage", null)]
    public async Task AnIncompatibleHost_IsRefusedAsync(string hostVersion, string? min, string? max)
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-reg-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", PluginSigningTestSeeds.SignedZip(pluginId), minHostVersion: min, maxHostVersion: max);
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry, hostVersion: hostVersion);

        var response = await client.PostAsJsonAsync(Route(pluginId), PluginRegistryTestSeeds.InstallBody("1.0.0"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REGISTRY_HOST_INCOMPATIBLE);
        registry.DownloadCount.Should().Be(0);
        Directory.Exists(PluginsTestSeeds.InstalledFolder(factory, pluginId)).Should().BeFalse();
        (await RefusalAuditedAsync(pluginId, "host version")).Should().BeTrue();
    }

    /// <summary>A version whose host range holds the running host installs.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ACompatibleHost_InstallsAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-reg-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", PluginSigningTestSeeds.SignedZip(pluginId), minHostVersion: "1.0.0", maxHostVersion: "2.0.0");
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry, hostVersion: "1.4.0");

        try
        {
            var response = await client.PostAsJsonAsync(Route(pluginId), PluginRegistryTestSeeds.InstallBody("1.0.0"));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>A version older than the installed one is refused with 409 unless the body allows it, and an omitted flag means no.</summary>
    /// <param name="flag">The <c>allowDowngrade</c> member of the body; <see langword="null"/> leaves it out.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task ADowngrade_IsRefusedWithoutTheFlagAsync(bool? flag)
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-reg-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", PluginSigningTestSeeds.SignedZip(pluginId));
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        try
        {
            (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "2.0.0"))).StatusCode.Should().Be(HttpStatusCode.OK);

            var response = await client.PostAsJsonAsync(Route(pluginId), PluginRegistryTestSeeds.InstallBody("1.0.0", flag));

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_DOWNGRADE_REFUSED);
            Directory.Exists(PluginsTestSeeds.StagedFolder(factory, pluginId)).Should().BeFalse();
            (await RefusalAuditedAsync(pluginId, "older than the installed version")).Should().BeTrue();
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>With <c>allowDowngrade</c> the older version is staged for the restart and the audit line says so.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ADowngrade_IsStagedWhenAllowedAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-reg-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", PluginSigningTestSeeds.SignedZip(pluginId));
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        try
        {
            await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "2.0.0"));

            var response = await client.PostAsJsonAsync(Route(pluginId), PluginRegistryTestSeeds.InstallBody("1.0.0", allowDowngrade: true));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("requiresRestart").GetBoolean().Should().BeTrue();
            body.GetProperty("stagedVersion").GetString().Should().Be("1.0.0");
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.ActivityLogs.AsNoTracking().AnyAsync(l =>
                l.Description.StartsWith("Forced Plugin Downgrade From Registry") && l.Description.Contains(pluginId))).Should().BeTrue();
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>The flag does not turn a newer version into anything else: it is still an upgrade.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AllowDowngrade_DoesNotChangeAnUpgradeAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-reg-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "2.0.0", PluginRegistryTestSeeds.SignedVersionedZip(pluginId, "2.0.0"));
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        try
        {
            await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(pluginId));

            var response = await client.PostAsJsonAsync(Route(pluginId), PluginRegistryTestSeeds.InstallBody("2.0.0", allowDowngrade: true));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.ActivityLogs.AsNoTracking().AnyAsync(l =>
                l.Description.StartsWith("Staged Plugin Upgrade From Registry") && l.Description.Contains(pluginId))).Should().BeTrue();
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }
}
