using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Plugins;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Innovayse.Hostpanel.Integration.Tests.Fixtures.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>POST /api/admin/plugins/install</c> and the plugin signature: a package signed by a trusted key installs; on a
/// host that enforces signatures an unsigned, tampered or unknown-key package is refused with its own localised
/// code and nothing is left behind, unless the admin setting "allow unsigned plugins" is on; a Development or Testing
/// host installs an unsigned package without the setting.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class InstallPluginSignatureTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A package signed by a trusted key installs on a host that enforces signatures.</summary>
    [Fact]
    public async Task SignedPackage_InstallsOnAnEnforcingHostAsync()
    {
        var client = await EnforcingClientAsync();
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";

        try
        {
            var response = await PluginsTestSeeds.InstallAsync(client, PluginSigningTestSeeds.SignedZip(pluginId));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            File.Exists(Path.Combine(PluginsTestSeeds.InstalledFolder(pluginId), PluginSignatureFile.FileName)).Should().BeTrue();
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>
    /// An unsigned, tampered or unknown-key package is refused with the code of its rule, nothing is installed, and
    /// the message is the localised text of the code in the caller's language.
    /// </summary>
    /// <param name="flaw">What is wrong with the package.</param>
    /// <param name="code">The error code the rule answers with.</param>
    [Theory]
    [InlineData(PluginPackageFlaw.Unsigned, "PLUGIN_UNSIGNED")]
    [InlineData(PluginPackageFlaw.Tampered, "PLUGIN_SIGNATURE_INVALID")]
    [InlineData(PluginPackageFlaw.UnknownKey, "PLUGIN_SIGNATURE_KEY_UNKNOWN")]
    public async Task AnEnforcingHost_RefusesAFlawedPackageAsync(PluginPackageFlaw flaw, string code)
    {
        var client = await EnforcingClientAsync();
        client.DefaultRequestHeaders.Add("X-Language", "ru");
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";

        var response = await PluginsTestSeeds.InstallAsync(client, FlawedZip(pluginId, flaw));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(code);
        body.RootElement.GetProperty("detail").GetString().Should().MatchRegex("[А-Яа-яЁё]").And.NotContain("PLUGIN_");
        Directory.Exists(PluginsTestSeeds.InstalledFolder(pluginId)).Should().BeFalse();
    }

    /// <summary>A <c>plugin.sig</c> that cannot be read is refused as <c>PLUGIN_SIGNATURE_MALFORMED</c>, in Armenian when asked.</summary>
    [Fact]
    public async Task AnEnforcingHost_RefusesAnUnreadableSignatureAsync()
    {
        var client = await EnforcingClientAsync();
        client.DefaultRequestHeaders.Add("X-Language", "hy");
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        var zip = PluginsTestSeeds.BuildValidPluginZip(
            pluginId, new Dictionary<string, string> { [PluginSignatureFile.FileName] = "not a signature" });

        var response = await PluginsTestSeeds.InstallAsync(client, zip);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("type").GetString().Should().Be(HostpanelErrorCodes.PLUGIN_SIGNATURE_MALFORMED);
        body.RootElement.GetProperty("detail").GetString().Should().MatchRegex("[Ա-Ֆա-ֆ]");
        Directory.Exists(PluginsTestSeeds.InstalledFolder(pluginId)).Should().BeFalse();
    }

    /// <summary>
    /// With the admin setting "allow unsigned plugins" on, an enforcing host installs an unsigned package; turned off
    /// again, the very next unsigned upload is refused.
    /// </summary>
    [Fact]
    public async Task TheAllowUnsignedSetting_AdmitsAnUnsignedPackageUntilItIsTurnedOffAsync()
    {
        var client = await EnforcingClientAsync();
        var settingId = await AdminTestSeeds.FindSettingIdAsync(client, PluginSettingKeys.AllowUnsigned);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";

        try
        {
            (await client.PutAsJsonAsync($"/api/admin/settings/{settingId}", new { value = PluginSettingKeys.On }))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            (await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(pluginId)))
                .StatusCode.Should().Be(HttpStatusCode.OK);

            (await client.PutAsJsonAsync($"/api/admin/settings/{settingId}", new { value = PluginSettingKeys.Off }))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            var refused = await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(pluginId));
            refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            await PluginsTestSeeds.AssertCodeAsync(refused, HostpanelErrorCodes.PLUGIN_UNSIGNED);
        }
        finally
        {
            await client.PutAsJsonAsync($"/api/admin/settings/{settingId}", new { value = PluginSettingKeys.Off });
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>The setting is seeded off, so an enforcing host refuses an unsigned package out of the box.</summary>
    [Fact]
    public async Task TheAllowUnsignedSetting_IsSeededOffAsync()
    {
        var client = await EnforcingClientAsync();

        var rows = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/settings");

        rows!.Single(r => r.GetProperty("key").GetString() == PluginSettingKeys.AllowUnsigned)
            .GetProperty("value").GetString().Should().Be(PluginSettingKeys.Off);
    }

    /// <summary>A Development or Testing host installs an unsigned package without the setting.</summary>
    [Fact]
    public async Task ADevelopmentHost_InstallsAnUnsignedPackageAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";

        try
        {
            var response = await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(pluginId));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>An admin client of a host that trusts the test key and enforces signatures, as Production does.</summary>
    /// <returns>An authenticated admin client.</returns>
    private async Task<HttpClient> EnforcingClientAsync()
    {
        WebApplicationFactory<Program> host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<PluginSignatureVerifier>();
            services.AddSingleton(PluginSigningTestSeeds.Verifier(enforced: true));
        }));

        return await PluginsTestSeeds.AdminClientAsync(factory, host);
    }

    /// <summary>Builds a plugin archive with one flaw.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <param name="flaw">The flaw.</param>
    /// <returns>The archive's raw bytes.</returns>
    private static byte[] FlawedZip(string pluginId, PluginPackageFlaw flaw)
    {
        return flaw switch
        {
            PluginPackageFlaw.Tampered => PluginSigningTestSeeds.SignedZip(
                pluginId, afterSigning: folder => File.WriteAllText(Path.Combine(folder, "readme.txt"), "changed after signing")),
            PluginPackageFlaw.UnknownKey => PluginSigningTestSeeds.SignedZip(
                pluginId, PluginSigningTestSeeds.UntrustedKeyId, PluginSigningTestSeeds.UntrustedKey.PrivateKey),
            _ => PluginsTestSeeds.BuildValidPluginZip(pluginId),
        };
    }
}
