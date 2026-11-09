using System.Net;
using System.Text;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common;
using Innovayse.Hostpanel.Application.Admin.Plugins.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Plugins;

/// <summary>
/// The registry client over a fake registry server, for a plugin that only a verified installation may have: the version list,
/// the version details, the download ticket and the package fetch are signed with the installation's identity and carry no
/// licence key; the registry's refusals become admin codes; a public plugin is asked for as before; the unsigned lookup of the
/// newest versions, which names no package address for a restricted plugin, is not an error. The host is the real one, only the
/// network under its registry clients is the fake server.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PluginRegistryClientSignedCallTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A licence key a test passes in, to prove it is not sent for a restricted plugin.</summary>
    private const string LicenceKey = "LICENCE-KEY-must-not-be-sent";

    /// <summary>Makes a fake registry and a host on it.</summary>
    /// <returns>The registry and the host.</returns>
    private (FakeRegistryServer Registry, RegistryTestHost Host) NewHost()
    {
        var registry = new FakeRegistryServer();
        return (registry, new RegistryTestHost(factory, registry));
    }

    /// <summary>Registers and verifies the installation of a host through the real enrolment.</summary>
    /// <param name="scope">A scope of the host.</param>
    /// <returns>A task that completes once the installation is verified.</returns>
    private static async Task EnrolAsync(IServiceScope scope)
    {
        var outcome = await scope.ServiceProvider.GetRequiredService<RegistryInstanceEnrollment>().EnsureVerifiedAsync(force: true, CancellationToken.None);
        outcome.IsSuccess.Should().BeTrue(outcome.ErrorCode);
    }

    /// <summary>A signed call carries the four headers, a timestamp near now and a nonce in the accepted shape, and never a licence key.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheVersionList_OfARestrictedPlugin_IsSigned_AndCarriesNoLicenceKeyAsync()
    {
        var (registry, host) = NewHost();
        using var hostToDispose = host;
        using var scope = host.Services.CreateScope();
        registry.Offer("acme-pay", "1.2.0", [1, 2, 3], hostOnly: true);
        await EnrolAsync(scope);
        var client = scope.ServiceProvider.GetRequiredService<IPluginRegistryClient>();

        var result = await client.ListVersionsAsync("acme-pay", LicenceKey, CancellationToken.None, PluginRegistryDistribution.HostOnly);

        result.IsSuccess.Should().BeTrue(result.ErrorCode);
        result.Value!.Single().Version.Should().Be("1.2.0");
        var call = registry.Calls.Last(c => c.Target == "/v1/plugins/acme-pay/versions");
        call.IsSigned.Should().BeTrue();
        call.HasLicenceKey.Should().BeFalse();
        call.Headers["X-Hostpanel-Instance"].Should().Be(registry.Instances.Single().Id.ToString("D"));
        long.Parse(call.Headers["X-Hostpanel-Timestamp"]).Should().BeCloseTo(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 30);
        call.Headers["X-Hostpanel-Nonce"].Length.Should().BeInRange(16, 64);
        Convert.FromBase64String(call.Headers["X-Hostpanel-Signature"]).Should().HaveCount(64);
    }

    /// <summary>Two calls never share a nonce, so the registry's replay check never refuses an honest host.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EveryCall_CarriesAFreshNonceAsync()
    {
        var (registry, host) = NewHost();
        using var hostToDispose = host;
        using var scope = host.Services.CreateScope();
        registry.Offer("acme-pay", "1.2.0", [1], hostOnly: true);
        await EnrolAsync(scope);
        var client = scope.ServiceProvider.GetRequiredService<IPluginRegistryClient>();

        for (var i = 0; i < 3; i++)
        {
            (await client.ListVersionsAsync("acme-pay", null, CancellationToken.None, PluginRegistryDistribution.HostOnly)).IsSuccess.Should().BeTrue();
        }

        var nonces = registry.Calls.Where(c => c.IsSigned).Select(c => c.Headers["X-Hostpanel-Nonce"]).ToList();
        nonces.Should().HaveCount(3).And.OnlyHaveUniqueItems();
    }

    /// <summary>The version details come from a signed call, name the checksum and carry the distribution to the download.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheRelease_OfARestrictedPlugin_IsSigned_AndKeepsItsDistributionAsync()
    {
        var (registry, host) = NewHost();
        using var hostToDispose = host;
        using var scope = host.Services.CreateScope();
        var package = Encoding.UTF8.GetBytes("package");
        registry.Offer("acme-pay", "1.2.0", package, hostOnly: true);
        await EnrolAsync(scope);
        var client = scope.ServiceProvider.GetRequiredService<IPluginRegistryClient>();

        var result = await client.GetReleaseAsync(
            new RegistryPluginVersionRef("acme-pay", "1.2.0", PluginRegistryDistribution.HostOnly), CancellationToken.None, LicenceKey);

        result.IsSuccess.Should().BeTrue(result.ErrorCode);
        result.Value!.Distribution.Should().Be(PluginRegistryDistribution.HostOnly);
        result.Value.Sha256.Should().Be(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(package)));
        var call = registry.Calls.Last(c => c.Target == "/v1/plugins/acme-pay/versions/1.2.0");
        call.IsSigned.Should().BeTrue();
        call.HasLicenceKey.Should().BeFalse();
    }

    /// <summary>The package is fetched through a signed ticket request and a signed fetch of the ticket address, in that order.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ARestrictedPackage_IsFetchedThroughATicket_BothCallsSignedAsync()
    {
        var (registry, host) = NewHost();
        using var hostToDispose = host;
        using var scope = host.Services.CreateScope();
        var package = Encoding.UTF8.GetBytes("the package bytes");
        registry.Offer("acme-pay", "1.2.0", package, hostOnly: true);
        await EnrolAsync(scope);
        var client = scope.ServiceProvider.GetRequiredService<IPluginRegistryClient>();
        var release = (await client.GetReleaseAsync(
            new RegistryPluginVersionRef("acme-pay", "1.2.0", PluginRegistryDistribution.HostOnly), CancellationToken.None, LicenceKey)).Value!;

        var downloaded = await client.DownloadAsync(release, CancellationToken.None, LicenceKey);

        downloaded.IsSuccess.Should().BeTrue(downloaded.ErrorCode);
        await using var stream = downloaded.Value!;
        using var copy = new MemoryStream();
        await stream.CopyToAsync(copy);
        copy.ToArray().Should().Equal(package);
        var calls = registry.Calls.Where(c => c.Target.Contains("/download")).ToList();
        calls.Select(c => (c.Method, Path: c.Target.Split('?')[0])).Should().Equal(
            ("POST", "/v1/plugins/acme-pay/versions/1.2.0/download-tickets"),
            ("GET", "/v1/plugins/acme-pay/versions/1.2.0/download"));
        calls.Should().OnlyContain(c => c.IsSigned && !c.HasLicenceKey);
        calls[1].Target.Should().Contain("?ticket=");
    }

    /// <summary>A ticket is good for one fetch, so every download asks for a ticket of its own, with a different token each time.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task EveryDownload_AsksForATicketOfItsOwnAsync()
    {
        var (registry, host) = NewHost();
        using var hostToDispose = host;
        using var scope = host.Services.CreateScope();
        registry.Offer("acme-pay", "1.2.0", [1, 2], hostOnly: true);
        await EnrolAsync(scope);
        var client = scope.ServiceProvider.GetRequiredService<IPluginRegistryClient>();
        var release = (await client.GetReleaseAsync(
            new RegistryPluginVersionRef("acme-pay", "1.2.0", PluginRegistryDistribution.HostOnly), CancellationToken.None)).Value!;

        for (var i = 0; i < 2; i++)
        {
            var downloaded = await client.DownloadAsync(release, CancellationToken.None);
            downloaded.IsSuccess.Should().BeTrue(downloaded.ErrorCode);
            await downloaded.Value!.DisposeAsync();
        }

        registry.Calls.Count(c => c.Method == "POST" && c.Target.EndsWith("/download-tickets", StringComparison.Ordinal)).Should().Be(2);
        registry.Calls.Where(c => c.Target.Contains("?ticket=")).Select(c => c.Target).Should().OnlyHaveUniqueItems().And.HaveCount(2);
    }

    /// <summary>A public plugin is asked for as before: no signature, and the licence key when one is given.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APublicPlugin_IsAskedForAsBefore_WithTheLicenceKeyAndNoSignatureAsync()
    {
        var (registry, host) = NewHost();
        using var hostToDispose = host;
        using var scope = host.Services.CreateScope();
        registry.Offer("free-plugin", "1.0.0", [9], hostOnly: false);
        var client = scope.ServiceProvider.GetRequiredService<IPluginRegistryClient>();

        var versions = await client.ListVersionsAsync("free-plugin", LicenceKey, CancellationToken.None);
        var release = await client.GetReleaseAsync(new RegistryPluginVersionRef("free-plugin", "1.0.0"), CancellationToken.None, LicenceKey);
        await using var package = (await client.DownloadAsync(release.Value!, CancellationToken.None, LicenceKey)).Value!;

        versions.IsSuccess.Should().BeTrue(versions.ErrorCode);
        release.Value!.Distribution.Should().Be(PluginRegistryDistribution.Download);
        registry.Calls.Where(c => c.Target.Contains("/free-plugin/")).Should().OnlyContain(c => !c.IsSigned && c.HasLicenceKey);
        registry.Calls.Should().NotContain(c => c.Target.Contains("download-tickets"));
    }

    /// <summary>An installation the registry has not verified signs nothing: the call is not made and the code says so.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WithoutAVerifiedIdentity_NoSignedCallIsMade_AndTheCodeSaysSoAsync()
    {
        var (registry, host) = NewHost();
        using var hostToDispose = host;
        using var scope = host.Services.CreateScope();
        registry.Offer("acme-pay", "1.2.0", [1], hostOnly: true);
        var client = scope.ServiceProvider.GetRequiredService<IPluginRegistryClient>();

        var result = await client.ListVersionsAsync("acme-pay", null, CancellationToken.None, PluginRegistryDistribution.HostOnly);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_NOT_VERIFIED);
        registry.Calls.Should().BeEmpty();
    }

    /// <summary>The registry's refusals of a signed call become the admin codes, from the registry's error code and the status.</summary>
    /// <param name="status">The status the registry answers with.</param>
    /// <param name="registryCode">The registry error code in the body, or an empty string for a body with none.</param>
    /// <param name="expected">The host code.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(401, "INSTANCE_SIGNATURE_INVALID_UNAUTHORIZED", "PLUGIN_REGISTRY_INSTANCE_SIGNATURE_REJECTED")]
    [InlineData(401, "INSTANCE_TIMESTAMP_UNAUTHORIZED", "PLUGIN_REGISTRY_INSTANCE_CLOCK_SKEW")]
    [InlineData(401, "INSTANCE_REPLAY_UNAUTHORIZED", "PLUGIN_REGISTRY_INSTANCE_REPLAY_REJECTED")]
    [InlineData(401, "", "PLUGIN_REGISTRY_INSTANCE_SIGNATURE_REJECTED")]
    [InlineData(403, "INSTANCE_NOT_VERIFIED_FORBIDDEN", "PLUGIN_REGISTRY_INSTANCE_NOT_VERIFIED")]
    [InlineData(403, "INSTANCE_REVOKED_FORBIDDEN", "PLUGIN_REGISTRY_INSTANCE_REVOKED")]
    [InlineData(403, "DOWNLOAD_TICKET_INVALID_FORBIDDEN", "PLUGIN_REGISTRY_DOWNLOAD_TICKET_REFUSED")]
    [InlineData(403, "VERSION_COMPROMISED_FORBIDDEN", "PLUGIN_REGISTRY_VERSION_COMPROMISED")]
    [InlineData(403, "PLUGIN_BLOCKED_FORBIDDEN", "PLUGIN_REGISTRY_UNAVAILABLE")]
    [InlineData(429, "INSTANCE_RATE_LIMIT_EXCEEDED", "PLUGIN_REGISTRY_INSTANCE_LIMIT_REACHED")]
    [InlineData(429, "", "PLUGIN_REGISTRY_INSTANCE_LIMIT_REACHED")]
    public async Task ARefusedSignedCall_BecomesAnAdminCodeAsync(int status, string registryCode, string expected)
    {
        var (registry, host) = NewHost();
        using var hostToDispose = host;
        using var scope = host.Services.CreateScope();
        registry.Offer("acme-pay", "1.2.0", [1], hostOnly: true);
        await EnrolAsync(scope);
        registry.Intercept = call => call.IsSigned && call.Target == "/v1/plugins/acme-pay/versions"
            ? new HttpResponseMessage((HttpStatusCode)status)
            {
                Content = new StringContent(registryCode.Length == 0 ? "not json" : $$"""{"type":"{{registryCode}}","detail":"secret registry text"}""", Encoding.UTF8, "application/json"),
            }
            : null;
        var client = scope.ServiceProvider.GetRequiredService<IPluginRegistryClient>();

        var result = await client.ListVersionsAsync("acme-pay", null, CancellationToken.None, PluginRegistryDistribution.HostOnly);

        result.ErrorCode.Should().Be(expected);
        result.Error.Should().BeNull("nothing the registry said is passed on");
    }

    /// <summary>A registry that finds the clock of this server too far off is answered with the clock code, and the real window decides it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AClockOutsideTheWindow_IsRefusedWithTheClockCodeAsync()
    {
        var (registry, host) = NewHost();
        using var hostToDispose = host;
        using var scope = host.Services.CreateScope();
        registry.Offer("acme-pay", "1.2.0", [1], hostOnly: true);
        await EnrolAsync(scope);
        registry.ClockOffset = TimeSpan.FromMinutes(20);
        var client = scope.ServiceProvider.GetRequiredService<IPluginRegistryClient>();

        var result = await client.ListVersionsAsync("acme-pay", null, CancellationToken.None, PluginRegistryDistribution.HostOnly);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_CLOCK_SKEW);
    }

    /// <summary>A revocation is remembered: the status turns Revoked and the next signed call is not made at all.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ARevocation_IsRememberedOnTheIdentityAsync()
    {
        var (registry, host) = NewHost();
        using var hostToDispose = host;
        using var scope = host.Services.CreateScope();
        registry.Offer("acme-pay", "1.2.0", [1], hostOnly: true);
        await EnrolAsync(scope);
        registry.Revoke(registry.Instances.Single().Id);
        var client = scope.ServiceProvider.GetRequiredService<IPluginRegistryClient>();

        var first = await client.ListVersionsAsync("acme-pay", null, CancellationToken.None, PluginRegistryDistribution.HostOnly);
        var signedCalls = registry.Calls.Count(c => c.IsSigned);
        var second = await client.ListVersionsAsync("acme-pay", null, CancellationToken.None, PluginRegistryDistribution.HostOnly);

        first.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_REVOKED);
        second.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_INSTANCE_REVOKED);
        registry.Calls.Count(c => c.IsSigned).Should().Be(signedCalls);
        (await scope.ServiceProvider.GetRequiredService<RegistryInstanceEnrollment>().GetStatusAsync(CancellationToken.None))
            .State.Should().Be(RegistryInstanceState.Revoked);
    }

    /// <summary>A ticket address on another host is refused before anything is fetched from it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ATicketAddressOnAnotherHost_IsRefusedAsync()
    {
        var (registry, host) = NewHost();
        using var hostToDispose = host;
        using var scope = host.Services.CreateScope();
        registry.Offer("acme-pay", "1.2.0", [1], hostOnly: true);
        await EnrolAsync(scope);
        registry.Intercept = call => call.Method == "POST" && call.Target.EndsWith("/download-tickets", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"ticket":"abc","downloadUrl":"https://evil.example.net/v1/plugins/acme-pay/versions/1.2.0/download?ticket=abc"}""", Encoding.UTF8, "application/json"),
            }
            : null;
        var client = scope.ServiceProvider.GetRequiredService<IPluginRegistryClient>();
        var release = (await client.GetReleaseAsync(
            new RegistryPluginVersionRef("acme-pay", "1.2.0", PluginRegistryDistribution.HostOnly), CancellationToken.None)).Value!;

        var result = await client.DownloadAsync(release, CancellationToken.None);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.PLUGIN_REGISTRY_PACKAGE_REFUSED);
        registry.Calls.Count(c => c.Method == "GET" && c.Target.Contains("download")).Should().Be(0);
    }

    /// <summary>The newest-version lookup is unsigned and an empty package address for a restricted plugin is not invalid there.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheLatestLookup_IsUnsigned_AndAnEmptyAddressIsNotAnErrorAsync()
    {
        var (registry, host) = NewHost();
        using var hostToDispose = host;
        using var scope = host.Services.CreateScope();
        registry.Offer("acme-pay", "1.2.0", [1], hostOnly: true);
        registry.Offer("free-plugin", "2.0.0", [2], hostOnly: false);
        var client = scope.ServiceProvider.GetRequiredService<IPluginRegistryClient>();

        var result = await client.GetLatestAsync(["acme-pay", "free-plugin"], CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.ErrorCode);
        result.Value!.Select(p => (p.Id, p.LatestVersion)).Should().BeEquivalentTo([("acme-pay", "1.2.0"), ("free-plugin", "2.0.0")]);
        registry.Calls.Single(c => c.Target == "/v1/plugins/latest").IsSigned.Should().BeFalse();
    }

    /// <summary>The listing and the plugin page carry the distribution, and a registry that sends none reads as a public plugin.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheListingAndThePluginPage_ReadTheDistributionAsync()
    {
        var (registry, host) = NewHost();
        using var hostToDispose = host;
        using var scope = host.Services.CreateScope();
        registry.Offer("acme-pay", "1.2.0", [1], hostOnly: true);
        registry.Offer("free-plugin", "2.0.0", [2], hostOnly: false);
        var client = scope.ServiceProvider.GetRequiredService<IPluginRegistryClient>();

        var page = await client.SearchAsync(new RegistryPluginSearch(null, null, 1, 20), CancellationToken.None);
        var detail = await client.GetPluginAsync("acme-pay", null, CancellationToken.None);

        page.Value!.Items.ToDictionary(p => p.Id, p => p.Distribution).Should().Equal(new Dictionary<string, PluginRegistryDistribution>
        {
            ["acme-pay"] = PluginRegistryDistribution.HostOnly,
            ["free-plugin"] = PluginRegistryDistribution.Download,
        });
        detail.Value!.Summary.Distribution.Should().Be(PluginRegistryDistribution.HostOnly);
        registry.Calls.Should().OnlyContain(c => !c.IsSigned);
    }
}
