using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary><c>POST api/admin/servers/{id}/ssh-settings/scan-host-key</c>: show, compare, never pin.</summary>
/// <param name="factory">The shared API and PostgreSQL host.</param>
public sealed class ScanServerSshHostKeyTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Gets the fake scanner the shared host resolves.</summary>
    private FakeSshHostKeyScanner Scanner => (FakeSshHostKeyScanner)factory.Services.GetRequiredService<ISshHostKeyScanner>();

    /// <summary>The presented host key is shown and compared with the pin, and the scan never pins it.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task PresentedKey_IsShownAndComparedWithThePinAsync()
    {
        var admin = await SshAdminTestSupport.AdminAsync(factory);
        var serverId = await SshTestSeeds.SeedServerAsync(factory, "scan.example.test", 22, enabled: false);
        var host = $"scan-{serverId}.internal.test";
        (await admin.PutAsJsonAsync($"/api/admin/servers/{serverId}/ssh-settings", SshAdminTestSupport.Body(false, host: host))).EnsureSuccessStatusCode();

        Scanner.Keys[$"{host}:22"] = new SshHostKeyScan("ssh-ed25519", SshAdminTestSupport.Fingerprint);
        var matching = await (await admin.PostAsync($"/api/admin/servers/{serverId}/ssh-settings/scan-host-key", null)).Content.ReadFromJsonAsync<JsonElement>();
        Scanner.Keys[$"{host}:22"] = new SshHostKeyScan("ssh-ed25519", "SHA256:" + new string('Z', 43));
        var different = await (await admin.PostAsync($"/api/admin/servers/{serverId}/ssh-settings/scan-host-key", null)).Content.ReadFromJsonAsync<JsonElement>();

        matching.GetProperty("matchesPinned").GetBoolean().Should().BeTrue();
        different.GetProperty("matchesPinned").GetBoolean().Should().BeFalse();
        different.GetProperty("algorithm").GetString().Should().Be("ssh-ed25519");
        var settings = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/servers/{serverId}/ssh-settings");
        settings.GetProperty("agentHostKeyFingerprint").GetString().Should().Be(SshAdminTestSupport.Fingerprint);
    }

    /// <summary>Each way the endpoint can fail names what to check, as an admin-facing 503.</summary>
    /// <param name="failure">How the endpoint failed.</param>
    /// <param name="code">The error code the admin sees.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(SshEndpointFailure.NoAnswer, HostpanelErrorCodes.SSH_AGENT_HOST_UNREACHABLE)]
    [InlineData(SshEndpointFailure.Refused, HostpanelErrorCodes.SSH_AGENT_PORT_REFUSED)]
    [InlineData(SshEndpointFailure.ClosedBeforeGreeting, HostpanelErrorCodes.SSH_AGENT_CONNECTION_CLOSED)]
    [InlineData(SshEndpointFailure.Other, HostpanelErrorCodes.SSH_HOST_KEY_UNREADABLE)]
    public async Task FailedRead_Returns503NamingWhatToCheckAsync(SshEndpointFailure failure, string code)
    {
        var admin = await SshAdminTestSupport.AdminAsync(factory);
        var serverId = await SshTestSeeds.SeedServerAsync(factory, "down.example.test", 22, enabled: false);
        var host = $"down-{serverId}.internal.test";
        (await admin.PutAsJsonAsync($"/api/admin/servers/{serverId}/ssh-settings", SshAdminTestSupport.Body(false, host: host))).EnsureSuccessStatusCode();
        Scanner.Failures[$"{host}:22"] = failure;

        var response = await admin.PostAsync($"/api/admin/servers/{serverId}/ssh-settings/scan-host-key", null);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(code);
    }

    /// <summary>A server with no agent host saved answers <c>SSH_SETTINGS_INCOMPLETE</c>.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task NoAgentHostSaved_IsIncompleteAsync()
    {
        var admin = await SshAdminTestSupport.AdminAsync(factory);
        var created = await admin.PostAsJsonAsync(
            "/api/admin/servers",
            ServersTestSeeds.ServerBody($"bare-{Guid.NewGuid():N}", "bare.example.test", Domain.Servers.Enums.ServerModule.Cwp7));
        created.EnsureSuccessStatusCode();
        var serverId = await created.Content.ReadFromJsonAsync<int>();

        var response = await admin.PostAsync($"/api/admin/servers/{serverId}/ssh-settings/scan-host-key", null);

        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.SSH_SETTINGS_INCOMPLETE);
    }
}
