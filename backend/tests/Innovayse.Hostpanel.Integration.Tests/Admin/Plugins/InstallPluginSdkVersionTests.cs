using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>POST /api/admin/plugins/install</c> against the package's <c>sdkPackageVersion</c>: a plugin built on the SDK the
/// host
/// ships (or an older one, or one that states none) installs; one built on a newer SDK is refused with
/// <c>PLUGIN_SDK_NEWER_THAN_HOST</c> (409) in the caller's language and nothing is installed; a value that is not a
/// version
/// and a <c>minHostVersion</c> below the first host that ships the SDK are refused as manifest rules (400).
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class InstallPluginSdkVersionTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>, IDisposable
{
    /// <summary>
    /// This class's own upload folder, so a staged upgrade of another test class never meets these tests.
    /// </summary>
    private readonly string _installRoot = Path.Combine(Path.GetTempPath(), $"hostpanel-sdkversion-{Guid.NewGuid():N}");

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

    /// <summary>
    /// A plugin built on the SDK the host ships installs, and so does one that states no SDK version.
    /// </summary>
    /// <param name="members">The manifest members to add; empty for none.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("")]
    [InlineData(",\"sdkPackageVersion\":\"HOST\",\"minHostVersion\":\"1.0.0\"")]
    public async Task APluginTheHostCanLoad_InstallsAsync(string members)
    {
        var (response, pluginId) = await InstallAsync(members.Replace("HOST", HostSdkVersion(), StringComparison.Ordinal));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        Directory.Exists(Path.Combine(_installRoot, pluginId)).Should().BeTrue();
    }

    /// <summary>
    /// A plugin built on a newer SDK is refused with the localised sentence and nothing is installed.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APluginBuiltOnANewerSdk_IsRefusedAsync()
    {
        var (response, pluginId) = await InstallAsync(",\"sdkPackageVersion\":\"999.0.0\",\"minHostVersion\":\"1.0.0\"", language: "ru");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.PLUGIN_SDK_NEWER_THAN_HOST);
        body.RootElement.GetProperty("detail").GetString().Should().MatchRegex("[А-Яа-яЁё]").And.NotContain("PLUGIN_");
        Directory.Exists(Path.Combine(_installRoot, pluginId)).Should().BeFalse();
    }

    /// <summary>A value that is not a version is refused as a manifest rule.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ASdkPackageVersionThatIsNotAVersion_IsRefusedAsync()
    {
        var (response, _) = await InstallAsync(",\"sdkPackageVersion\":\"three\"");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_MANIFEST_SDK_PACKAGE_VERSION_INVALID);
    }

    /// <summary>A minimum host below the first host that ships the SDK is refused as a manifest rule.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AMinHostBelowTheFirstHostShippingTheSdk_IsRefusedAsync()
    {
        var (response, _) = await InstallAsync(",\"sdkPackageVersion\":\"3.1.0\",\"minHostVersion\":\"0.1.0\"");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_MANIFEST_MIN_HOST_BELOW_SDK);
    }

    /// <summary>Reads the SDK version the host under test ships, from the container it runs in.</summary>
    /// <returns>The version as <c>major.minor.patch</c>.</returns>
    private string HostSdkVersion()
    {
        return Host.Services.GetRequiredService<IHostSdkVersionProvider>().Version;
    }

    /// <summary>Installs a fresh plugin whose manifest carries the given members.</summary>
    /// <param name="members">The manifest members, each starting with a comma; empty for none.</param>
    /// <param name="language">The <c>X-Language</c> header to send, or <see langword="null"/> for none.</param>
    /// <returns>The response and the plugin id.</returns>
    private async Task<(HttpResponseMessage Response, string PluginId)> InstallAsync(string members, string? language = null)
    {
        var client = await AuthTestSeeds.AdminClientAsync(factory, Host);
        if (language is not null)
        {
            client.DefaultRequestHeaders.Add("X-Language", language);
        }

        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        var zip = PluginsTestSeeds.BuildZip(new Dictionary<string, string> { ["plugin.json"] = PluginsTestSeeds.ManifestJson(pluginId, members) });
        return (await PluginsTestSeeds.InstallAsync(client, zip), pluginId);
    }
}
