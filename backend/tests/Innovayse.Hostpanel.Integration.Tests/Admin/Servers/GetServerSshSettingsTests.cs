using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Servers;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary><c>GET api/admin/servers/{id}/ssh-settings</c>.</summary>
public sealed class GetServerSshSettingsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A configured server answers its settings and never includes the private key.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ConfiguredServer_ReturnsSettingsWithoutThePrivateKeyAsync()
    {
        var admin = await SshAdminTestSupport.AdminAsync(factory);
        var serverId = await SshTestSeeds.SeedServerAsync(factory, "get.example.test", 2200);

        var response = await admin.GetAsync($"/api/admin/servers/{serverId}/ssh-settings");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain("PRIVATE-KEY-PLACEHOLDER");
        var body = JsonDocument.Parse(raw).RootElement;
        body.GetProperty("isSupported").GetBoolean().Should().BeTrue();
        body.GetProperty("sshPort").GetInt32().Should().Be(2200);
    }

    /// <summary>A server without settings on an unsupported module answers disabled, unsupported defaults without a key.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UnconfiguredUnsupportedServer_AnswersDisabledDefaultsAsync()
    {
        var admin = await SshAdminTestSupport.AdminAsync(factory);
        var created = await admin.PostAsJsonAsync(
            "/api/admin/servers",
            ServersTestSeeds.ServerBody($"plain-{Guid.NewGuid():N}", hostname: "plain.example.test", module: ServerModules.CPanel));
        created.EnsureSuccessStatusCode();
        var serverId = await created.Content.ReadFromJsonAsync<int>();

        var body = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/servers/{serverId}/ssh-settings");

        body.GetProperty("isEnabled").GetBoolean().Should().BeFalse();
        body.GetProperty("isSupported").GetBoolean().Should().BeFalse();
        body.GetProperty("hasAgentPrivateKey").GetBoolean().Should().BeFalse();
    }

    /// <summary>An unknown server id answers 404 with the server-not-found code.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UnknownServer_Returns404Async()
    {
        var admin = await SshAdminTestSupport.AdminAsync(factory);

        var response = await admin.GetAsync("/api/admin/servers/2147483000/ssh-settings");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.SERVER_NOT_FOUND);
    }
}
