using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary><c>PUT api/admin/servers/{id}/ssh-settings</c>: saving, the verify-before-enable gate and endpoint uniqueness.</summary>
public sealed class ConfigureServerSshTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Saves, tests and enables a server's settings on one agent host, as the setup dialog does.</summary>
    /// <param name="admin">The administrator's client.</param>
    /// <param name="serverId">The server.</param>
    /// <param name="host">The agent host to save.</param>
    /// <returns>The answer to the enabling save.</returns>
    private static async Task<HttpResponseMessage> SaveTestEnableAsync(HttpClient admin, int serverId, string host)
    {
        (await admin.PutAsJsonAsync($"/api/admin/servers/{serverId}/ssh-settings", SshAdminTestSupport.Body(false, host: host))).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/admin/servers/{serverId}/ssh-settings/test", null)).EnsureSuccessStatusCode();
        return await admin.PutAsJsonAsync($"/api/admin/servers/{serverId}/ssh-settings", SshAdminTestSupport.Body(true, privateKey: null, host: host));
    }

    /// <summary>Saving settings answers 200 without the private key, and the stored column holds ciphertext, not the key.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Save_EncryptsTheKeyAndNeverReturnsItAsync()
    {
        var admin = await SshAdminTestSupport.AdminAsync(factory);
        var serverId = await SshTestSeeds.SeedServerAsync(factory, "x.example.test", 22, enabled: false);

        var response = await admin.PutAsJsonAsync($"/api/admin/servers/{serverId}/ssh-settings", SshAdminTestSupport.Body(false));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain("TEST-PRIVATE-KEY-MATERIAL");
        var body = JsonDocument.Parse(raw).RootElement;
        body.GetProperty("sshPort").GetInt32().Should().Be(2222);
        body.GetProperty("hasAgentPrivateKey").GetBoolean().Should().BeTrue();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var connection = (NpgsqlConnection)((DbContext)context).Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT \"AgentPrivateKey\" FROM \"ServerSshSettings\" WHERE \"ServerId\" = @id", connection);
        command.Parameters.AddWithValue("id", serverId);
        ((string?)await command.ExecuteScalarAsync()).Should().NotBeNull().And.NotContain("TEST-PRIVATE-KEY-MATERIAL");
    }

    /// <summary>Enabling settings that were never tested successfully answers 409 <c>SSH_AGENT_NOT_VERIFIED</c>.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Enable_WithoutASuccessfulTest_IsRefusedAsync()
    {
        var admin = await SshAdminTestSupport.AdminAsync(factory);
        var serverId = await SshTestSeeds.SeedServerAsync(factory, "unverified.example.test", 22, enabled: false);

        var response = await admin.PutAsJsonAsync($"/api/admin/servers/{serverId}/ssh-settings", SshAdminTestSupport.Body(true));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.SSH_AGENT_NOT_VERIFIED);
    }

    /// <summary>Enabling after a successful test succeeds and keeps the stored key when none is sent.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Enable_AfterASuccessfulTest_SucceedsAndKeepsTheStoredKeyAsync()
    {
        var admin = await SshAdminTestSupport.AdminAsync(factory);
        var serverId = await SshTestSeeds.SeedServerAsync(factory, "verified.example.test", 22, enabled: false);

        var response = await SaveTestEnableAsync(admin, serverId, $"agent-{serverId}-{Guid.NewGuid():N}.example.test");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isEnabled").GetBoolean().Should().BeTrue();
        body.GetProperty("hasAgentPrivateKey").GetBoolean().Should().BeTrue();
    }

    /// <summary>Changing the agent endpoint clears the last test, so enabling is refused until it is tested again.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ChangingTheEndpoint_ClearsTheTestAndBlocksEnablingAsync()
    {
        var admin = await SshAdminTestSupport.AdminAsync(factory);
        var serverId = await SshTestSeeds.SeedServerAsync(factory, "moved.example.test", 22, enabled: false);
        (await admin.PutAsJsonAsync($"/api/admin/servers/{serverId}/ssh-settings", SshAdminTestSupport.Body(false, host: "a.example.test"))).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/admin/servers/{serverId}/ssh-settings/test", null)).EnsureSuccessStatusCode();

        var response = await admin.PutAsJsonAsync($"/api/admin/servers/{serverId}/ssh-settings", SshAdminTestSupport.Body(true, privateKey: null, host: "b.example.test"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.SSH_AGENT_NOT_VERIFIED);
    }

    /// <summary>Sending a new private key for an enabled, tested server is refused and leaves the stored key and health as they were.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ReplacingTheKeyOfAnEnabledServer_IsRefusedAndChangesNothingAsync()
    {
        var admin = await SshAdminTestSupport.AdminAsync(factory);
        var serverId = await SshTestSeeds.SeedServerAsync(factory, "rotate.example.test", 22, enabled: false);
        var host = $"rotate-{Guid.NewGuid():N}.example.test";
        (await SaveTestEnableAsync(admin, serverId, host)).EnsureSuccessStatusCode();
        var before = await StoredKeyAsync(serverId);

        var response = await admin.PutAsJsonAsync($"/api/admin/servers/{serverId}/ssh-settings", SshAdminTestSupport.Body(true, privateKey: "A-DIFFERENT-KEY", host: host));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.SSH_AGENT_NOT_VERIFIED);
        var settings = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/servers/{serverId}/ssh-settings");
        settings.GetProperty("isEnabled").GetBoolean().Should().BeTrue();
        settings.GetProperty("isHealthy").GetBoolean().Should().BeTrue();
        (await StoredKeyAsync(serverId)).Should().Be(before);
    }

    /// <summary>A second server enabled on an agent endpoint another enabled server uses answers 409 <c>SSH_AGENT_ENDPOINT_IN_USE</c>.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TwoServersOnOneAgentEndpoint_SecondIsRefusedAsync()
    {
        var admin = await SshAdminTestSupport.AdminAsync(factory);
        var host = $"shared-{Guid.NewGuid():N}.example.test";
        var first = await SshTestSeeds.SeedServerAsync(factory, "one.example.test", 22, enabled: false);
        var second = await SshTestSeeds.SeedServerAsync(factory, "two.example.test", 22, enabled: false);
        (await SaveTestEnableAsync(admin, first, host)).EnsureSuccessStatusCode();

        var response = await SaveTestEnableAsync(admin, second, host.ToUpperInvariant());

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.SSH_AGENT_ENDPOINT_IN_USE);
    }

    /// <summary>Enabling SSH access on a server whose module has no agent support answers 400 with the module error code.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task EnablingAnUnsupportedModule_IsRefusedAsync()
    {
        var admin = await SshAdminTestSupport.AdminAsync(factory);
        var serverId = await SshTestSeeds.SeedServerAsync(factory, "cp.example.test", 22, ServerModules.CPanel, enabled: false);

        var response = await admin.PutAsJsonAsync($"/api/admin/servers/{serverId}/ssh-settings", SshAdminTestSupport.Body(true));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.SSH_MODULE_NOT_SUPPORTED);
    }

    /// <summary>A host key fingerprint that is not SHA256 answers 400 with the validation error code.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task BadFingerprint_Returns400ValidationErrorAsync()
    {
        var admin = await SshAdminTestSupport.AdminAsync(factory);
        var serverId = await SshTestSeeds.SeedServerAsync(factory, "fp.example.test", 22, enabled: false);

        var response = await admin.PutAsJsonAsync($"/api/admin/servers/{serverId}/ssh-settings", new
        {
            isEnabled = false,
            sshHostname = "h",
            sshPort = 22,
            agentHost = "h",
            agentPort = 22,
            agentUsername = "hp",
            agentHostKeyFingerprint = "md5:nope",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>A signed-in client cannot configure server SSH settings.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Client_IsForbiddenAsync()
    {
        var (http, _) = await DomainsTestSeeds.RegisterOwnerAsync(factory, "ssh-admin-client");

        var response = await http.PutAsJsonAsync("/api/admin/servers/1/ssh-settings", SshAdminTestSupport.Body(true));

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
    }

    /// <summary>The stored (encrypted) private key column, compared as-is.</summary>
    /// <param name="serverId">The server.</param>
    /// <returns>The column value.</returns>
    private async Task<string?> StoredKeyAsync(int serverId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var connection = (NpgsqlConnection)((DbContext)context).Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT \"AgentPrivateKey\" FROM \"ServerSshSettings\" WHERE \"ServerId\" = @id", connection);
        command.Parameters.AddWithValue("id", serverId);
        return (string?)await command.ExecuteScalarAsync();
    }
}
