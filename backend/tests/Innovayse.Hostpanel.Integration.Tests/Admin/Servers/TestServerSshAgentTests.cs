using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Domain.Ssh.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Admin.Servers;

/// <summary><c>POST api/admin/servers/{id}/ssh-settings/test</c>.</summary>
/// <param name="factory">The shared API and PostgreSQL host.</param>
public sealed class TestServerSshAgentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A healthy agent reports its version and the health is recorded on the server settings.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task HealthyAgent_ReportsVersionAndRecordsHealthAsync()
    {
        var admin = await SshAdminTestSupport.AdminAsync(factory);
        var serverId = await SshTestSeeds.SeedServerAsync(factory, "ping.example.test", 22);

        var body = await (await admin.PostAsync($"/api/admin/servers/{serverId}/ssh-settings/test", null))
            .Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("isHealthy").GetBoolean().Should().BeTrue();
        body.GetProperty("agentVersion").GetString().Should().Be("1.1.0");
        body.GetProperty("provider").GetString().Should().Be(SshTestSeeds.BuiltInProfile);
        (await admin.GetFromJsonAsync<JsonElement>($"/api/admin/servers/{serverId}/ssh-settings"))
            .GetProperty("isHealthy").GetBoolean().Should().BeTrue();
    }

    /// <summary>An unreachable agent answers 200 with an unhealthy result carrying only the failure code.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UnreachableAgent_ReportsTheCodeOnlyAsync()
    {
        var admin = await SshAdminTestSupport.AdminAsync(factory);
        var serverId = await SshTestSeeds.SeedServerAsync(factory, "down.example.test", 22);
        SshTestSeeds.Agents(factory).AgentFor(serverId).FailWith = SshProviderFailure.Unreachable;

        var response = await admin.PostAsync($"/api/admin/servers/{serverId}/ssh-settings/test", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("isHealthy").GetBoolean().Should().BeFalse();
        body.GetProperty("error").GetString().Should().Be(nameof(SshProviderFailure.Unreachable));
        body.GetProperty("provider").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>
    /// An agent installed for another server record answers, but the check is unhealthy with
    /// <see cref="SshProviderFailure.BindingMismatch"/> and the health is recorded as failed.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AgentBoundToAnotherServer_IsUnhealthyWithBindingMismatchAsync()
    {
        var admin = await SshAdminTestSupport.AdminAsync(factory);
        var serverId = await SshTestSeeds.SeedServerAsync(factory, "bound.example.test", 22);
        SshTestSeeds.Agents(factory).AgentFor(serverId).Binding = Guid.NewGuid();

        var body = await (await admin.PostAsync($"/api/admin/servers/{serverId}/ssh-settings/test", null))
            .Content.ReadFromJsonAsync<JsonElement>();

        body.GetProperty("isHealthy").GetBoolean().Should().BeFalse();
        body.GetProperty("error").GetString().Should().Be(nameof(SshProviderFailure.BindingMismatch));
        body.GetProperty("agentVersion").GetString().Should().Be("1.1.0");
        body.GetProperty("provider").GetString().Should().Be(SshTestSeeds.BuiltInProfile);
        (await admin.GetFromJsonAsync<JsonElement>($"/api/admin/servers/{serverId}/ssh-settings"))
            .GetProperty("isHealthy").GetBoolean().Should().BeFalse();
    }
}
