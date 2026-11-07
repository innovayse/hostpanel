using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Plugins.Enums;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Plugins;

/// <summary>
/// <c>POST /api/admin/plugins/registry/{id}/install</c>: a verified registry package installs and is audited; the trust
/// setting is enforced (verified only, any publisher, operator keys only); a package that does not hash to the published
/// checksum, or is another plugin, is refused before it is unpacked; signatures are enforced even when the operator
/// allows unsigned uploads; registry failures keep their codes.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class InstallRegistryPluginTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The route of a registry install.</summary>
    /// <param name="pluginId">The plugin id.</param>
    /// <returns>The route.</returns>
    private static string Route(string pluginId) => $"/api/admin/plugins/registry/{pluginId}/install";

    /// <summary>A verified package installs under the default policy and the install is written to the activity log with its digest.</summary>
    [Fact]
    public async Task AVerifiedPackage_InstallsAndIsAuditedAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-reg-{Guid.NewGuid():N}";
        var zip = PluginSigningTestSeeds.SignedZip(pluginId);
        registry.Publish(pluginId, "1.0.0", zip);
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        try
        {
            var response = await client.PostAsJsonAsync(Route(pluginId), PluginRegistryTestSeeds.InstallBody("1.0.0"));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("requiresRestart").GetBoolean().Should().BeTrue();
            body.GetProperty("stagedVersion").ValueKind.Should().Be(JsonValueKind.Null);
            File.Exists(Path.Combine(PluginsTestSeeds.InstalledFolder(pluginId), "readme.txt")).Should().BeTrue();

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var line = await db.ActivityLogs.AsNoTracking().SingleAsync(l => l.Description.Contains(pluginId));
            line.ClientId.Should().BeNull();
            line.Description.Should().StartWith("Installed Plugin From Registry")
                .And.Contain($"Plugin ID: {pluginId} - Version: 1.0.0")
                .And.Contain(Convert.ToHexStringLower(SHA256.HashData(zip)));
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>A newer registry version of an installed plugin is staged for the restart.</summary>
    [Fact]
    public async Task ANewerVersion_IsStagedAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-reg-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "2.0.0", PluginRegistryTestSeeds.SignedVersionedZip(pluginId, "2.0.0"));
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        try
        {
            await PluginsTestSeeds.InstallAsync(client, PluginsTestSeeds.BuildValidPluginZip(pluginId));

            var response = await client.PostAsJsonAsync(Route(pluginId), PluginRegistryTestSeeds.InstallBody("2.0.0"));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("stagedVersion").GetString().Should().Be("2.0.0");
        }
        finally
        {
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>Under the default policy an unverified entry is refused before anything is downloaded, and the refusal is audited.</summary>
    [Fact]
    public async Task VerifiedOnly_RefusesAnUnverifiedEntryAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-reg-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", PluginSigningTestSeeds.SignedZip(pluginId), verified: false);
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.PostAsJsonAsync(Route(pluginId), PluginRegistryTestSeeds.InstallBody("1.0.0"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REGISTRY_NOT_TRUSTED);
        registry.DownloadCount.Should().Be(0);
        Directory.Exists(PluginsTestSeeds.InstalledFolder(pluginId)).Should().BeFalse();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ActivityLogs.AsNoTracking().AnyAsync(l => l.Description.StartsWith("Refused Plugin From Registry") && l.Description.Contains(pluginId))).Should().BeTrue();
    }

    /// <summary>Under "any registry publisher" an unverified entry installs.</summary>
    [Fact]
    public async Task AnyRegistryPublisher_AdmitsAnUnverifiedEntryAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-reg-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", PluginSigningTestSeeds.SignedZip(pluginId), verified: false);
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        try
        {
            await PluginRegistryTestSeeds.SetTrustAsync(client, PluginRegistryTrust.AnyRegistryPublisher);

            var response = await client.PostAsJsonAsync(Route(pluginId), PluginRegistryTestSeeds.InstallBody("1.0.0"));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            await PluginRegistryTestSeeds.SetTrustAsync(client, PluginRegistryTrust.VerifiedOnly);
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>
    /// Under "own keys only" a package signed with a key that is not in the operator's list is refused even though the
    /// registry verified it, and an unsigned one is refused too.
    /// </summary>
    /// <param name="signed">Whether the package carries a signature by a key outside the operator's list.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OwnKeysOnly_RefusesAKeyTheOperatorDidNotAddAsync(bool signed)
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-reg-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", signed ? PluginSigningTestSeeds.SignedZip(pluginId) : PluginsTestSeeds.BuildValidPluginZip(pluginId));
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        try
        {
            await PluginRegistryTestSeeds.SetTrustAsync(client, PluginRegistryTrust.OwnKeysOnly);

            var response = await client.PostAsJsonAsync(Route(pluginId), PluginRegistryTestSeeds.InstallBody("1.0.0"));

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REGISTRY_NOT_TRUSTED);
            Directory.Exists(PluginsTestSeeds.InstalledFolder(pluginId)).Should().BeFalse();
        }
        finally
        {
            await PluginRegistryTestSeeds.SetTrustAsync(client, PluginRegistryTrust.VerifiedOnly);
        }
    }

    /// <summary>Under "own keys only" a package signed with a key the operator added installs, verified by the registry or not.</summary>
    [Fact]
    public async Task OwnKeysOnly_AdmitsAnOperatorKeyAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-reg-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", PluginSigningTestSeeds.SignedZip(pluginId), verified: false);
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry, PluginSigningTestSeeds.TrustedEntry);

        try
        {
            await PluginRegistryTestSeeds.SetTrustAsync(client, PluginRegistryTrust.OwnKeysOnly);

            var response = await client.PostAsJsonAsync(Route(pluginId), PluginRegistryTestSeeds.InstallBody("1.0.0"));

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            await PluginRegistryTestSeeds.SetTrustAsync(client, PluginRegistryTrust.VerifiedOnly);
            await client.DeleteAsync($"/api/admin/plugins/{pluginId}");
        }
    }

    /// <summary>A package that does not hash to the published checksum is refused before it is unpacked, and the refusal is audited.</summary>
    [Fact]
    public async Task AChecksumMismatch_IsRefusedAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-reg-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", PluginSigningTestSeeds.SignedZip(pluginId), publishedSha256: new string('a', 64));
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.PostAsJsonAsync(Route(pluginId), PluginRegistryTestSeeds.InstallBody("1.0.0"));

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REGISTRY_CHECKSUM_MISMATCH);
        Directory.Exists(PluginsTestSeeds.InstalledFolder(pluginId)).Should().BeFalse();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ActivityLogs.AsNoTracking().AnyAsync(l => l.Description.Contains("checksum mismatch") && l.Description.Contains(pluginId))).Should().BeTrue();
    }

    /// <summary>A package whose manifest is another plugin than the one asked for is refused.</summary>
    [Fact]
    public async Task APackageOfAnotherPlugin_IsRefusedAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-reg-{Guid.NewGuid():N}";
        var otherId = $"test-other-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", PluginSigningTestSeeds.SignedZip(otherId));
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.PostAsJsonAsync(Route(pluginId), PluginRegistryTestSeeds.InstallBody("1.0.0"));

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REGISTRY_PACKAGE_REFUSED);
        Directory.Exists(PluginsTestSeeds.InstalledFolder(otherId)).Should().BeFalse();
    }

    /// <summary>
    /// A host that does not enforce signatures (Development, Testing) still refuses an unsigned registry package, even
    /// when the operator allows unsigned uploads: that setting is for uploads, never for the registry.
    /// </summary>
    [Fact]
    public async Task AnUnsignedRegistryPackage_IsRefusedEvenWhereSignaturesAreNotEnforcedAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-reg-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "1.0.0", PluginsTestSeeds.BuildValidPluginZip(pluginId));
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        try
        {
            await PluginRegistryTestSeeds.SetAsync(client, PluginSettingKeys.AllowUnsigned, PluginSettingKeys.On);

            var response = await client.PostAsJsonAsync(Route(pluginId), PluginRegistryTestSeeds.InstallBody("1.0.0"));

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_UNSIGNED);
            Directory.Exists(PluginsTestSeeds.InstalledFolder(pluginId)).Should().BeFalse();
        }
        finally
        {
            await PluginRegistryTestSeeds.SetAsync(client, PluginSettingKeys.AllowUnsigned, PluginSettingKeys.Off);
        }
    }

    /// <summary>A package that is signed by a key the host does not trust, or tampered with after signing, is refused.</summary>
    /// <param name="tampered">Whether the package is tampered with (otherwise it is signed by an unknown key).</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ABadSignature_IsRefusedAsync(bool tampered)
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-reg-{Guid.NewGuid():N}";
        var zip = tampered
            ? PluginSigningTestSeeds.SignedZip(pluginId, afterSigning: folder => File.WriteAllText(Path.Combine(folder, "readme.txt"), "changed"))
            : PluginSigningTestSeeds.SignedZip(pluginId, PluginSigningTestSeeds.UntrustedKeyId, PluginSigningTestSeeds.UntrustedKey.PrivateKey);
        registry.Publish(pluginId, "1.0.0", zip);
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.PostAsJsonAsync(Route(pluginId), PluginRegistryTestSeeds.InstallBody("1.0.0"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await PluginsTestSeeds.AssertCodeAsync(response, tampered ? HostpanelErrorCodes.PLUGIN_SIGNATURE_INVALID : HostpanelErrorCodes.PLUGIN_SIGNATURE_KEY_UNKNOWN);
        Directory.Exists(PluginsTestSeeds.InstalledFolder(pluginId)).Should().BeFalse();
    }

    /// <summary>A package whose manifest declares another version than the one asked for is refused before it is unpacked.</summary>
    [Fact]
    public async Task APackageOfAnotherVersion_IsRefusedAsync()
    {
        var registry = new FakePluginRegistryClient();
        var pluginId = $"test-reg-{Guid.NewGuid():N}";
        registry.Publish(pluginId, "2.0.0", PluginSigningTestSeeds.SignedZip(pluginId));
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.PostAsJsonAsync(Route(pluginId), PluginRegistryTestSeeds.InstallBody("2.0.0"));

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REGISTRY_PACKAGE_REFUSED);
        Directory.Exists(PluginsTestSeeds.InstalledFolder(pluginId)).Should().BeFalse();
    }

    /// <summary>A version the registry does not list answers 404, and a switched-off registry answers 409.</summary>
    /// <param name="failureCode">The registry's failure, or <see langword="null"/> for an unlisted version.</param>
    /// <param name="status">The status the code maps to.</param>
    [Theory]
    [InlineData(null, HttpStatusCode.NotFound)]
    [InlineData("PLUGIN_REGISTRY_DISABLED", HttpStatusCode.Conflict)]
    [InlineData("PLUGIN_REGISTRY_UNAVAILABLE", HttpStatusCode.BadGateway)]
    [InlineData("PLUGIN_REGISTRY_TIMED_OUT", HttpStatusCode.GatewayTimeout)]
    public async Task RegistryFailures_KeepTheirCodesAsync(string? failureCode, HttpStatusCode status)
    {
        var registry = new FakePluginRegistryClient { FailureCode = failureCode };
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.PostAsJsonAsync(Route("some-plugin"), PluginRegistryTestSeeds.InstallBody("9.9.9"));

        response.StatusCode.Should().Be(status);
        await PluginsTestSeeds.AssertCodeAsync(response, failureCode ?? HostpanelErrorCodes.PLUGIN_REGISTRY_PLUGIN_NOT_FOUND);
    }

    /// <summary>The real client with the default configuration answers <c>PLUGIN_REGISTRY_DISABLED</c>.</summary>
    [Fact]
    public async Task TheDefaultConfiguration_AnswersDisabledAsync()
    {
        var client = await PluginsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(Route("some-plugin"), PluginRegistryTestSeeds.InstallBody("1.0.0"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await PluginsTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PLUGIN_REGISTRY_DISABLED);
    }

    /// <summary>An id or version of the wrong shape is refused before the registry is asked.</summary>
    /// <param name="pluginId">The route id.</param>
    /// <param name="version">The body version.</param>
    [Theory]
    [InlineData("Bad_Id", "1.0.0")]
    [InlineData("good-id", "")]
    [InlineData("good-id", "not-a-version")]
    [InlineData("good-id", "1.0.0/../x")]
    public async Task AMalformedRequest_IsRefusedAsync(string pluginId, string version)
    {
        var registry = new FakePluginRegistryClient();
        var client = await PluginRegistryTestSeeds.AdminClientAsync(factory, registry);

        var response = await client.PostAsJsonAsync(Route(pluginId), PluginRegistryTestSeeds.InstallBody(version));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        registry.DownloadCount.Should().Be(0);
    }

    /// <summary>A Client-role caller is refused and a caller with no credential is unauthorised.</summary>
    [Fact]
    public async Task OnlyAnAdministrator_MayInstallAsync()
    {
        var clientRole = await PluginsTestSeeds.ClientRoleClientAsync(factory, "registry-install");
        (await clientRole.PostAsJsonAsync(Route("good-id"), PluginRegistryTestSeeds.InstallBody("1.0.0"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var anonymous = factory.CreateClient();
        (await anonymous.PostAsJsonAsync(Route("good-id"), PluginRegistryTestSeeds.InstallBody("1.0.0"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
