using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>POST /api/admin/plugins/inspect</c>: answers what an uploaded package asks for (host access, capabilities, changelog,
/// supported host versions) and whether this host is inside the range, without installing anything; an archive without a
/// manifest and a form without a file are refused; a caller who is not an administrator is refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class InspectPluginPackageTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The Hostpanel version the host reports in these tests.</summary>
    private const string HostVersion = "2.0.0";

    /// <summary>A package that declares every field is described in full and marked compatible, and nothing is installed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APackageInsideTheRange_IsDescribedAndNotInstalledAsync()
    {
        var pluginId = $"test-plugin-{Guid.NewGuid():N}";
        var manifest = PluginsTestSeeds.ManifestJson(
            pluginId,
            ",\"manifestVersion\":2,\"hostAccess\":[\"ssh\"],\"capabilities\":[\"endpoints\"],"
            + "\"changelog\":\"Fixed the renewal date.\",\"minHostVersion\":\"1.0.0\",\"maxHostVersion\":\"3.0.0\"",
            "App");

        var body = await InspectOkAsync(manifest);

        body.GetProperty("id").GetString().Should().Be(pluginId);
        body.GetProperty("version").GetString().Should().Be("1.0.0");
        body.GetProperty("hostAccess").EnumerateArray().Select(e => e.GetString()).Should().Equal("ssh");
        body.GetProperty("capabilities").EnumerateArray().Select(e => e.GetString()).Should().Equal("endpoints");
        body.GetProperty("changelog").GetString().Should().Be("Fixed the renewal date.");
        body.GetProperty("minHostVersion").GetString().Should().Be("1.0.0");
        body.GetProperty("maxHostVersion").GetString().Should().Be("3.0.0");
        body.GetProperty("hostVersion").GetString().Should().Be(HostVersion);
        body.GetProperty("compatible").GetBoolean().Should().BeTrue();
        Directory.Exists(PluginsTestSeeds.InstalledFolder(pluginId)).Should().BeFalse();
    }

    /// <summary>A package whose minimum is above this host is marked incompatible.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APackageBelowItsMinimum_IsMarkedIncompatibleAsync()
    {
        var body = await InspectOkAsync(PluginsTestSeeds.ManifestJson($"test-plugin-{Guid.NewGuid():N}", ",\"minHostVersion\":\"2.1.0\""));

        body.GetProperty("compatible").GetBoolean().Should().BeFalse();
    }

    /// <summary>A package whose maximum is below this host is marked incompatible.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APackageAboveItsMaximum_IsMarkedIncompatibleAsync()
    {
        var body = await InspectOkAsync(PluginsTestSeeds.ManifestJson($"test-plugin-{Guid.NewGuid():N}", ",\"maxHostVersion\":\"1.9.9\""));

        body.GetProperty("compatible").GetBoolean().Should().BeFalse();
    }

    /// <summary>A package without the new fields reads as empty lists, null texts and no limit.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APackageWithoutTheFields_HasNoLimitAsync()
    {
        var body = await InspectOkAsync(PluginsTestSeeds.ManifestJson($"test-plugin-{Guid.NewGuid():N}"));

        body.GetProperty("hostAccess").GetArrayLength().Should().Be(0);
        body.GetProperty("capabilities").GetArrayLength().Should().Be(0);
        body.GetProperty("changelog").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("minHostVersion").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("maxHostVersion").ValueKind.Should().Be(JsonValueKind.Null);
        body.GetProperty("compatible").GetBoolean().Should().BeTrue();
    }

    /// <summary>An archive without <c>plugin.json</c> is refused with <c>PLUGIN_MANIFEST_INVALID</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnArchiveWithoutAManifest_IsRefusedAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);

        var response = await PluginsTestSeeds.InspectAsync(client, PluginsTestSeeds.BuildZipWithoutManifest());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_MANIFEST_INVALID);
    }

    /// <summary>A form without a file is refused by the validator.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AFormWithoutAFile_IsRefusedAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("nothing"), "note");

        var response = await client.PostAsync("/api/admin/plugins/inspect", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A caller with only the Client role is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AClientRoleCaller_IsForbiddenAsync()
    {
        var client = await PluginsTestSeeds.ClientRoleClientAsync(factory, "inspect-plugin-forbidden");

        var response = await PluginsTestSeeds.InspectAsync(client, PluginsTestSeeds.BuildZipWithoutManifest());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Inspects a package holding only the given manifest and returns the body of the 200 answer.</summary>
    /// <param name="manifest">The manifest text.</param>
    /// <returns>The response body.</returns>
    private async Task<JsonElement> InspectOkAsync(string manifest)
    {
        factory.Services.GetRequiredService<FakeInstalledVersionProvider>().Version = HostVersion;
        var client = await PluginsTestSeeds.AdminClientAsync(factory);

        var response = await PluginsTestSeeds.InspectAsync(
            client, PluginsTestSeeds.BuildZip(new Dictionary<string, string> { ["plugin.json"] = manifest }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }
}
