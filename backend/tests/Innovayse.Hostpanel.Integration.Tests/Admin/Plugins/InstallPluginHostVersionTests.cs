using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>POST /api/admin/plugins/install</c> against the package's <c>minHostVersion</c> and <c>maxHostVersion</c>: a host
/// inside the range installs, a host below the minimum or above the maximum is refused with
/// <c>PLUGIN_HOST_VERSION_OUT_OF_RANGE</c> (409) and nothing is unpacked or staged, a bound the manifest leaves out is no
/// limit, a pre-release host ranks below its release, and an upgrade outside the range is refused the same way.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class InstallPluginHostVersionTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>, IDisposable
{
    /// <summary>The Hostpanel version the host reports in these tests unless a test sets another.</summary>
    private const string HostVersion = "2.0.0";

    /// <summary>This class's own upload folder, so a staged upgrade of another test class never meets these tests.</summary>
    private readonly string _installRoot = Path.Combine(Path.GetTempPath(), $"hostpanel-hostversion-{Guid.NewGuid():N}");

    /// <summary>The host that uses <see cref="_installRoot"/>, created on first use.</summary>
    private WebApplicationFactory<Program>? _host;

    /// <summary>A host derived from the shared one whose upload folder is <see cref="_installRoot"/>.</summary>
    private WebApplicationFactory<Program> Host =>
        _host ??= factory.WithWebHostBuilder(builder => builder.UseSetting("Plugins:InstallPath", _installRoot));

    /// <summary>Stops the host and deletes the upload folder.</summary>
    public void Dispose()
    {
        _host?.Dispose();
        if (Directory.Exists(_installRoot))
        {
            Directory.Delete(_installRoot, recursive: true);
        }
    }

    /// <summary>A host inside the declared range installs the plugin.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AHostInsideTheRange_InstallsAsync()
    {
        var (status, pluginId) = await InstallWithRangeAsync(",\"minHostVersion\":\"1.0.0\",\"maxHostVersion\":\"3.0.0\"");

        status.Should().Be(HttpStatusCode.OK);
        Directory.Exists(InstalledFolder(pluginId)).Should().BeTrue();
    }

    /// <summary>A host on the exact bounds is inside the range: both bounds are inclusive.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AHostOnTheBounds_InstallsAsync()
    {
        var (status, _) = await InstallWithRangeAsync($",\"minHostVersion\":\"{HostVersion}\",\"maxHostVersion\":\"{HostVersion}\"");

        status.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A host below the minimum is refused with the localised sentence and nothing is installed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AHostBelowTheMinimum_IsRefusedAsync()
    {
        await AssertOutOfRangeAsync(",\"minHostVersion\":\"2.1.0\"");
    }

    /// <summary>A host above the maximum is refused with the localised sentence and nothing is installed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AHostAboveTheMaximum_IsRefusedAsync()
    {
        await AssertOutOfRangeAsync(",\"maxHostVersion\":\"1.9.9\"");
    }

    /// <summary>A manifest without either bound sets no limit.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AManifestWithoutBounds_InstallsAsync()
    {
        var (status, _) = await InstallWithRangeAsync(string.Empty);

        status.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>Only a maximum, with the host below it: the missing minimum is no limit.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OnlyAMaximumAboveTheHost_InstallsAsync()
    {
        var (status, _) = await InstallWithRangeAsync(",\"maxHostVersion\":\"9.0.0\"");

        status.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>A pre-release host ranks below its release, so a minimum of that release refuses it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APreReleaseHost_RanksBelowItsReleaseAsync()
    {
        var (status, pluginId) = await InstallWithRangeAsync(",\"minHostVersion\":\"2.0.0\"", hostVersion: "2.0.0-rc.1");

        status.Should().Be(HttpStatusCode.Conflict);
        Directory.Exists(InstalledFolder(pluginId)).Should().BeFalse();
    }

    /// <summary>An upgrade whose new version does not run on this host is refused and nothing is staged.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnUpgradeOutsideTheRange_IsRefusedAndNotStagedAsync()
    {
        var client = await ClientAsync(HostVersion);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildVersionedPluginZip(pluginId, "1.0.0")))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var upgrade = PluginsTestSeeds.BuildZip(new Dictionary<string, string>
        {
            ["plugin.json"] = PluginsTestSeeds.ManifestJson(pluginId, ",\"minHostVersion\":\"3.0.0\"")
                .Replace("\"version\": \"1.0.0\"", "\"version\": \"1.1.0\"", StringComparison.Ordinal),
        });
        var response = await PluginsTestSeeds.InstallAsync(client, upgrade);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_HOST_VERSION_OUT_OF_RANGE);
        Directory.Exists(Path.Combine(_installRoot, ".staged", pluginId)).Should().BeFalse();
    }

    /// <summary>Refuses a manifest with the given range and checks the code, the Russian sentence and that nothing was installed.</summary>
    /// <param name="range">The range members, each starting with a comma.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    private async Task AssertOutOfRangeAsync(string range)
    {
        var client = await ClientAsync(HostVersion);
        client.DefaultRequestHeaders.Add("X-Language", "ru");
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        var zip = PluginsTestSeeds.BuildZip(new Dictionary<string, string> { ["plugin.json"] = PluginsTestSeeds.ManifestJson(pluginId, range) });

        var response = await PluginsTestSeeds.InstallAsync(client, zip);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.PLUGIN_HOST_VERSION_OUT_OF_RANGE);
        body.RootElement.GetProperty("detail").GetString().Should().MatchRegex("[А-Яа-яЁё]").And.NotContain("PLUGIN_");
        Directory.Exists(InstalledFolder(pluginId)).Should().BeFalse();
    }

    /// <summary>Installs a fresh plugin whose manifest carries the given range members.</summary>
    /// <param name="range">The range members, each starting with a comma; empty for none.</param>
    /// <param name="hostVersion">The Hostpanel version the host reports.</param>
    /// <returns>The response status and the plugin id.</returns>
    private async Task<(HttpStatusCode Status, string PluginId)> InstallWithRangeAsync(string range, string hostVersion = HostVersion)
    {
        var client = await ClientAsync(hostVersion);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        var zip = PluginsTestSeeds.BuildZip(new Dictionary<string, string> { ["plugin.json"] = PluginsTestSeeds.ManifestJson(pluginId, range) });

        var response = await PluginsTestSeeds.InstallAsync(client, zip);
        return (response.StatusCode, pluginId);
    }

    /// <summary>An admin client of <see cref="Host"/>, with the host reporting the given version.</summary>
    /// <param name="hostVersion">The Hostpanel version the host reports.</param>
    /// <returns>The authenticated client.</returns>
    private async Task<HttpClient> ClientAsync(string hostVersion)
    {
        Host.Services.GetRequiredService<FakeInstalledVersionProvider>().Version = hostVersion;
        return await PluginsTestSeeds.AdminClientAsync(factory, Host);
    }

    /// <summary>The folder an uploaded plugin is unpacked into under this class's upload folder.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The absolute folder path.</returns>
    private string InstalledFolder(string pluginId)
    {
        return Path.Combine(_installRoot, pluginId);
    }
}
