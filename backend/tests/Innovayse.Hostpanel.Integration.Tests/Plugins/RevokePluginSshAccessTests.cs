using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Host;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// <see cref="IPluginHostApi.RevokeSshAccessAsync"/>: a plugin takes back only keys it was issued, even after its product
/// is unbound; the client's own keys, another plugin's key, an unknown key and another client's service answer NotFound;
/// a manifest without SSH and the admin area are NotAllowed; an unreachable agent is ServerUnavailable; and the client can
/// revoke a plugin's key through its own route. Supersedes drafts-758's copy: the test drafts' cases folded in.
/// </summary>
/// <param name="fixture">The host with the SSH and the plain plugin.</param>
public sealed class RevokePluginSshAccessTests(PluginSshFixture fixture) : IClassFixture<PluginSshFixture>
{
    /// <summary>The plugin revokes its own key: it leaves the account, the record is revoked and the revoke is audited.</summary>
    [Fact]
    public async Task OwnKey_IsRevokedAndAuditedAsync()
    {
        var (owner, keyId) = await IssuedAsync("plugin-ssh-revoke");

        await fixture.AsPluginAsync(PluginSshFixture.ClientCall(fixture.SshPluginId, owner.ClientId), async api =>
        {
            await api.RevokeSshAccessAsync(owner.ServiceId, keyId, CancellationToken.None);
            return true;
        });

        fixture.Agents().AgentFor(owner.ServerId).KeysOf("web").Should().BeEmpty();
        using var scope = fixture.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.SshKeys.SingleAsync(k => k.KeyId == keyId)).SyncState.Should().Be(SshKeySyncState.Revoked);
        (await fixture.ActivityOfAsync(owner.ClientId)).Should()
            .ContainSingle(line => line.StartsWith($"Plugin {fixture.SshPluginId} revoked SSH key \"deploy\"", StringComparison.Ordinal))
            .Which.Should().EndWith($"from service #{owner.ServiceId}");
        (await RefusalAsync(PluginSshFixture.ClientCall(fixture.SshPluginId, owner.ClientId), owner.ServiceId, keyId))
            .Should().Be(PluginHostAccessFailure.NotFound, "a revoked key is no longer the plugin's to revoke");
    }

    /// <summary>A client's own key, and a key of another client, answer NotFound and stay on the account.</summary>
    [Fact]
    public async Task ClientsOwnKey_IsNotFoundAsync()
    {
        var owner = await fixture.OwnerWithServiceAsync("plugin-ssh-clientkey");
        await fixture.BindProductsOfAsync(fixture.SshPluginId, owner.ServiceId);
        using var http = AppPluginFixture.WithBearerOf(fixture.Host, owner.Http);
        var clientKey = await SshTestSeeds.AddKeyAsync(http, owner.ServiceId, 7);

        var failure = await RefusalAsync(PluginSshFixture.ClientCall(fixture.SshPluginId, owner.ClientId), owner.ServiceId, clientKey);

        failure.Should().Be(PluginHostAccessFailure.NotFound);
        fixture.Agents().AgentFor(owner.ServerId).KeysOf("web").Should().ContainSingle();
    }

    /// <summary>Unbinding the product does not stop the plugin taking its key back.</summary>
    [Fact]
    public async Task UnboundAfterIssue_StillRevokesAsync()
    {
        var (owner, keyId) = await IssuedAsync("plugin-ssh-unbound-revoke");
        await fixture.BindProductsOfAsync(fixture.SshPluginId);

        await fixture.AsPluginAsync(PluginSshFixture.ClientCall(fixture.SshPluginId, owner.ClientId), async api =>
        {
            await api.RevokeSshAccessAsync(owner.ServiceId, keyId, CancellationToken.None);
            return true;
        });

        fixture.Agents().AgentFor(owner.ServerId).KeysOf("web").Should().BeEmpty();
    }

    /// <summary>The client revokes a plugin's key through its own route like any other key.</summary>
    [Fact]
    public async Task Client_CanRevokeAPluginsKeyAsync()
    {
        var (owner, keyId) = await IssuedAsync("plugin-ssh-client-revokes");
        using var http = AppPluginFixture.WithBearerOf(fixture.Host, owner.Http);

        var response = await http.DeleteAsync($"/api/me/services/{owner.ServiceId}/ssh/keys/{keyId}");

        response.EnsureSuccessStatusCode();
        fixture.Agents().AgentFor(owner.ServerId).KeysOf("web").Should().BeEmpty();
    }

    /// <summary>Another plugin with SSH access cannot revoke a key issued to the first one; an unknown key answers the same.</summary>
    [Fact]
    public async Task AnotherPluginsKey_IsNotFound_LikeAnUnknownKeyAsync()
    {
        var (owner, keyId) = await IssuedAsync("plugin-ssh-other-plugin");
        await fixture.BindProductsOfAsync(fixture.OtherSshPluginId, owner.ServiceId);
        var other = PluginSshFixture.ClientCall(fixture.OtherSshPluginId, owner.ClientId);

        (await RefusalAsync(other, owner.ServiceId, keyId)).Should().Be(PluginHostAccessFailure.NotFound);
        (await RefusalAsync(other, owner.ServiceId, Guid.NewGuid())).Should().Be(PluginHostAccessFailure.NotFound);
        fixture.Agents().AgentFor(owner.ServerId).KeysOf("web").Keys.Should().Contain(keyId);
    }

    /// <summary>A call acting for another client answers NotFound for a key on a service it does not own.</summary>
    [Fact]
    public async Task AnotherClientsService_IsNotFoundAsync()
    {
        var (owner, keyId) = await IssuedAsync("plugin-ssh-revoke-owner");
        var stranger = await fixture.OwnerWithServiceAsync("plugin-ssh-revoke-stranger");

        var failure = await RefusalAsync(PluginSshFixture.ClientCall(fixture.SshPluginId, stranger.ClientId), owner.ServiceId, keyId);

        failure.Should().Be(PluginHostAccessFailure.NotFound);
        (await fixture.KeyAsync(keyId))!.SyncState.Should().Be(SshKeySyncState.Installed);
    }

    /// <summary>A plugin whose manifest does not declare SSH, and an admin-area call, are NotAllowed to revoke; the key stays.</summary>
    [Fact]
    public async Task ManifestWithoutSshOrAdminCall_IsNotAllowedAsync()
    {
        var (owner, keyId) = await IssuedAsync("plugin-ssh-revoke-notallowed");

        (await RefusalAsync(PluginSshFixture.ClientCall(fixture.PlainPluginId, owner.ClientId), owner.ServiceId, keyId))
            .Should().Be(PluginHostAccessFailure.NotAllowed);
        (await RefusalAsync(PluginCallBinding.WithoutClient(fixture.SshPluginId), owner.ServiceId, keyId))
            .Should().Be(PluginHostAccessFailure.NotAllowed);
        (await fixture.KeyAsync(keyId))!.SyncState.Should().Be(SshKeySyncState.Installed);
    }

    /// <summary>An unreachable agent answers ServerUnavailable and the key is not recorded as revoked.</summary>
    [Fact]
    public async Task UnreachableServer_IsServerUnavailableAsync()
    {
        var (owner, keyId) = await IssuedAsync("plugin-ssh-revoke-down");
        fixture.Agents().AgentFor(owner.ServerId).FailWith = SshProviderFailure.Unreachable;

        var failure = await RefusalAsync(PluginSshFixture.ClientCall(fixture.SshPluginId, owner.ClientId), owner.ServiceId, keyId);

        failure.Should().Be(PluginHostAccessFailure.ServerUnavailable);
        (await fixture.KeyAsync(keyId))!.SyncState.Should().NotBe(SshKeySyncState.Revoked);
    }

    /// <summary>A contact without <see cref="ClientPermission.ViewModifyPasswords"/> may not revoke through a plugin either; the key stays.</summary>
    [Fact]
    public async Task ContactWithoutThePasswordsRight_IsNotAllowedAsync()
    {
        var (owner, keyId) = await IssuedAsync("plugin-ssh-revoke-contact");
        var contact = PluginSshFixture.ClientCall(
            fixture.SshPluginId, owner.ClientId, ClientPermission.All & ~ClientPermission.ViewModifyPasswords);

        (await RefusalAsync(contact, owner.ServiceId, keyId)).Should().Be(PluginHostAccessFailure.NotAllowed);
        (await fixture.KeyAsync(keyId))!.SyncState.Should().Be(SshKeySyncState.Installed);
        fixture.Agents().AgentFor(owner.ServerId).KeysOf("web").Keys.Should().Contain(keyId);
    }

    /// <summary>A plugin can take its key back from a suspended service: removing access never needs the service usable.</summary>
    [Fact]
    public async Task InactiveService_StillRevokesAsync()
    {
        var (owner, keyId) = await IssuedAsync("plugin-ssh-inactive-revoke");
        await SshTestSeeds.ChangeServiceQuietlyAsync(
            fixture.Factory, owner.ServiceId, s => s.Suspend(DateTimeOffset.UtcNow, hostingAlreadyUpdated: true));

        await fixture.AsPluginAsync(PluginSshFixture.ClientCall(fixture.SshPluginId, owner.ClientId), async api =>
        {
            await api.RevokeSshAccessAsync(owner.ServiceId, keyId, CancellationToken.None);
            return true;
        });

        fixture.Agents().AgentFor(owner.ServerId).KeysOf("web").Should().BeEmpty();
        (await fixture.KeyAsync(keyId))!.SyncState.Should().Be(SshKeySyncState.Revoked);
    }

    /// <summary>Issues a key to the SSH plugin on a fresh bound service.</summary>
    /// <param name="label">Label for the generated e-mail.</param>
    /// <returns>The owner and the issued key id.</returns>
    private async Task<IssuedPluginSshKey> IssuedAsync(string label)
    {
        var owner = await fixture.OwnerWithServiceAsync(label);
        await fixture.BindProductsOfAsync(fixture.SshPluginId, owner.ServiceId);
        var access = await fixture.AsPluginAsync(
            PluginSshFixture.ClientCall(fixture.SshPluginId, owner.ClientId),
            api => api.IssueSshAccessAsync(owner.ServiceId, "deploy", CancellationToken.None));
        return new IssuedPluginSshKey(owner, access.KeyId);
    }

    /// <summary>Calls revoke and returns the refusal the plugin catches.</summary>
    /// <param name="binding">The call.</param>
    /// <param name="serviceId">The service.</param>
    /// <param name="keyId">The key.</param>
    /// <returns>The failure the host reported.</returns>
    private async Task<PluginHostAccessFailure> RefusalAsync(PluginCallBinding binding, int serviceId, Guid keyId)
    {
        var refused = await fixture.AsPluginAsync(binding, async api =>
        {
            var act = () => api.RevokeSshAccessAsync(serviceId, keyId, CancellationToken.None);
            return (await act.Should().ThrowAsync<PluginHostAccessException>()).Which;
        });
        return refused.Failure;
    }
}
