using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary><c>DELETE api/me/services/{id}/ssh/keys/{keyId}</c>: removal, cross-service refusals and migration safety.</summary>
public sealed class RevokeMySshKeyTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Revoking an own key answers 204, removes it from its server, marks it revoked, logs it as revoked, and a second revoke answers 404.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task OwnKey_IsRemovedFromItsServerAndMarkedRevokedAsync()
    {
        var (http, clientId, serverId, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "ssh-revoke-ok");
        var keyId = await SshTestSeeds.AddKeyAsync(http, serviceId, 1);

        var response = await http.DeleteAsync($"/api/me/services/{serviceId}/ssh/keys/{keyId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        SshTestSeeds.Agents(factory).AgentFor(serverId).KeysOf("web").Should().BeEmpty();
        (await KeyAsync(keyId)).SyncState.Should().Be(SshKeySyncState.Revoked);
        (await http.DeleteAsync($"/api/me/services/{serviceId}/ssh/keys/{keyId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.ActivityLogs.AnyAsync(l => l.ClientId == clientId && l.Description.Contains("Revoked SSH key"))).Should().BeTrue();
    }

    /// <summary>A key revoked through another service of the same client answers 404 and nothing is sent to the server.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task KeyOfAnotherOwnService_IsNotFoundAndNothingIsSentAsync()
    {
        var (http, clientId, serverId, serviceA) = await SshTestSeeds.OwnerWithServiceAsync(factory, "ssh-revoke-cross");
        var serviceB = await SshTestSeeds.SeedServiceAsync(factory, clientId, serverId, "other");
        var keyId = await SshTestSeeds.AddKeyAsync(http, serviceA, 1);
        var before = SshTestSeeds.Agents(factory).AgentFor(serverId).Calls.Count;

        var response = await http.DeleteAsync($"/api/me/services/{serviceB}/ssh/keys/{keyId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.SSH_KEY_NOT_FOUND);
        SshTestSeeds.Agents(factory).AgentFor(serverId).Calls.Count.Should().Be(before);
    }

    /// <summary>Another client's service answers 404 and the key stays installed.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AStrangersService_Returns404Async()
    {
        var (http, _, _, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "ssh-revoke-owner");
        var keyId = await SshTestSeeds.AddKeyAsync(http, serviceId, 1);
        var stranger = await DomainsTestSeeds.RegisterStrangerAsync(factory, "ssh-revoke-stranger");

        var response = await stranger.DeleteAsync($"/api/me/services/{serviceId}/ssh/keys/{keyId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await KeyAsync(keyId)).SyncState.Should().Be(SshKeySyncState.Installed);
    }

    /// <summary>After the service moves to another server the key is stale, the revoke answers 409, and neither server receives a revoke.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ServiceMoved_KeyIsRevokedOnItsOldServerOnlyAsync()
    {
        var (http, _, oldServer, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "ssh-revoke-moved");
        var keyId = await SshTestSeeds.AddKeyAsync(http, serviceId, 1);
        var newServer = await SshTestSeeds.SeedServerAsync(factory, "new.example.test", 22);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            await context.ClientServices.Where(s => s.Id == serviceId)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.ServerId, newServer));
        }

        var response = await http.DeleteAsync($"/api/me/services/{serviceId}/ssh/keys/{keyId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        SshTestSeeds.Agents(factory).AgentFor(oldServer).KeysOf("web").Should().BeEmpty();
        SshTestSeeds.Agents(factory).AgentFor(newServer).Calls.Should().NotContain(c => c.StartsWith("revoke"));
        (await KeyAsync(keyId)).SyncState.Should().Be(SshKeySyncState.Revoked);
    }

    /// <summary>
    /// With SSH access switched off for the server, the customer can still revoke a key: the revoke
    /// answers 204 and the key is gone from the server.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SshSwitchedOffOnTheServer_CustomerCanStillRevokeAsync()
    {
        var (http, _, serverId, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "ssh-revoke-off");
        var keyId = await SshTestSeeds.AddKeyAsync(http, serviceId, 1);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            await context.ServerSshSettings.Where(s => s.ServerId == serverId)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.IsEnabled, false));
        }

        var response = await http.DeleteAsync($"/api/me/services/{serviceId}/ssh/keys/{keyId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        SshTestSeeds.Agents(factory).AgentFor(serverId).KeysOf("web").Should().BeEmpty();
    }

    /// <summary>A key can still be revoked while shell access is disabled.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ShellDisabled_RevokeStillWorksAsync()
    {
        var (http, _, serverId, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "ssh-revoke-noshell");
        var keyId = await SshTestSeeds.AddKeyAsync(http, serviceId, 1);
        SshTestSeeds.Agents(factory).AgentFor(serverId).ShellEnabled = false;

        (await http.DeleteAsync($"/api/me/services/{serviceId}/ssh/keys/{keyId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>An unreachable server answers 503, marks the revoke as failed and keeps the key active.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UnreachableServer_MarksRevokeFailedAndKeepsTheKeyActiveAsync()
    {
        var (http, _, serverId, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "ssh-revoke-down");
        var keyId = await SshTestSeeds.AddKeyAsync(http, serviceId, 1);
        SshTestSeeds.Agents(factory).AgentFor(serverId).FailWith = SshProviderFailure.Unreachable;

        var response = await http.DeleteAsync($"/api/me/services/{serviceId}/ssh/keys/{keyId}");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var key = await KeyAsync(keyId);
        key.SyncState.Should().Be(SshKeySyncState.RevokeFailed);
        key.IsActive.Should().BeTrue();
        key.LastSyncError.Should().Be(SshProviderFailure.Unreachable);
    }

    /// <summary>Loads a stored key record by its key id.</summary>
    /// <param name="keyId">The key id.</param>
    /// <returns>The key record.</returns>
    private async Task<SshKey> KeyAsync(Guid keyId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.SshKeys.AsNoTracking().SingleAsync(k => k.KeyId == keyId);
    }
}
