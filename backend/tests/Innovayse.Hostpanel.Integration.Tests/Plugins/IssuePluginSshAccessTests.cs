using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Plugins.Common;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Settings;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Host;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Plugins;

/// <summary>
/// <see cref="IPluginHostApi.IssueSshAccessAsync"/> through <see cref="PluginSshAccessService"/>: each refusal in its
/// order, the key installed only on the service's own account and listed as the client's, the private key the pair of the
/// installed public key, the activity line, the issue budget (spent -> ServerUnavailable, limiter error -> fails closed), and the private key in no database column and no log line.
/// </summary>
/// <param name="fixture">The host with the SSH and the plain plugin.</param>
public sealed class IssuePluginSshAccessTests(PluginSshFixture fixture) : IClassFixture<PluginSshFixture>
{
    /// <summary>The label a plugin gives its key in these tests.</summary>
    private const string Label = "deploy";

    /// <summary>A bound service of the caller gets an identity on its own account, recorded as issued to the plugin and audited.</summary>
    [Fact]
    public async Task BoundService_IsIssuedAnIdentityOnItsOwnAccountAsync()
    {
        var owner = await fixture.OwnerWithServiceAsync("plugin-ssh-ok");
        await fixture.BindProductsOfAsync(fixture.SshPluginId, owner.ServiceId);

        var access = await fixture.AsPluginAsync(
            PluginSshFixture.ClientCall(fixture.SshPluginId, owner.ClientId),
            api => api.IssueSshAccessAsync(owner.ServiceId, $"  {Label} ", CancellationToken.None));

        access.Username.Should().Be("web");
        access.Host.Should().Be("plugin-ssh-ok.example.test");
        access.PrivateKey.Should().StartWith("-----BEGIN OPENSSH PRIVATE KEY-----");
        access.ToString().Should().NotContain(access.PrivateKey);
        var installed = fixture.Agents().AgentFor(owner.ServerId).KeysOf("web");
        installed.Keys.Should().Equal(access.KeyId);
        DecodedBody(access.PrivateKey).AsSpan().IndexOf(Convert.FromBase64String(installed[access.KeyId].Split(' ')[1]))
            .Should().BeGreaterThanOrEqualTo(0, "the private key handed to the plugin must be the pair of the installed public key");

        using var scope = fixture.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var key = await context.SshKeys.SingleAsync(k => k.KeyId == access.KeyId);
        key.IssuedToPluginId.Should().Be(fixture.SshPluginId);
        key.Label.Should().Be(Label);
        key.SyncState.Should().Be(SshKeySyncState.Installed);
        (await fixture.ActivityOfAsync(owner.ClientId)).Should()
            .ContainSingle(line => line.StartsWith($"Issued SSH key \"{Label}\"", StringComparison.Ordinal))
            .Which.Should().EndWith($"to plugin {fixture.SshPluginId} for service #{owner.ServiceId}");
    }

    /// <summary>The issued key is listed among the service's keys by the client's own route.</summary>
    [Fact]
    public async Task IssuedKey_IsListedForTheClientAsync()
    {
        var owner = await fixture.OwnerWithServiceAsync("plugin-ssh-list");
        await fixture.BindProductsOfAsync(fixture.SshPluginId, owner.ServiceId);
        var access = await fixture.AsPluginAsync(
            PluginSshFixture.ClientCall(fixture.SshPluginId, owner.ClientId),
            api => api.IssueSshAccessAsync(owner.ServiceId, Label, CancellationToken.None));

        using var http = AppPluginFixture.WithBearerOf(fixture.Host, owner.Http);
        var body = await http.GetStringAsync($"/api/me/services/{owner.ServiceId}/ssh/keys");

        body.Should().Contain(access.KeyId.ToString());
        body.Should().NotContain("PRIVATE KEY");
    }

    /// <summary>The private key is in no text, JSON or byte column of any table, the message store included, and in no log line.</summary>
    [Fact]
    public async Task PrivateKey_IsNeverStoredOrLoggedAsync()
    {
        var owner = await fixture.OwnerWithServiceAsync("plugin-ssh-secret");
        await fixture.BindProductsOfAsync(fixture.SshPluginId, owner.ServiceId);

        var access = await fixture.AsPluginAsync(
            PluginSshFixture.ClientCall(fixture.SshPluginId, owner.ClientId),
            api => api.IssueSshAccessAsync(owner.ServiceId, Label, CancellationToken.None));

        var material = PluginSshFixture.PrivateMaterialOf(access.PrivateKey);

        (await fixture.DatabaseHoldsAsync(material)).Should().BeFalse("the private key must not be persisted anywhere");
        (await fixture.DatabaseHoldsAsync(PluginSshFixture.PrivateKeyHeader)).Should().BeFalse();
        fixture.LoggedMessages.Should().NotContain(m => m.Contains(material) || m.Contains("PRIVATE KEY"));
    }

    /// <summary>A plugin whose manifest does not declare SSH is refused before anything is read or sent.</summary>
    [Fact]
    public async Task ManifestWithoutSsh_IsNotAllowedAsync()
    {
        var owner = await fixture.OwnerWithServiceAsync("plugin-ssh-nocap");
        await fixture.BindProductsOfAsync(fixture.PlainPluginId, owner.ServiceId);

        var failure = await RefusalAsync(PluginSshFixture.ClientCall(fixture.PlainPluginId, owner.ClientId), owner.ServiceId);

        failure.Should().Be(PluginHostAccessFailure.NotAllowed);
        fixture.Agents().AgentFor(owner.ServerId).Calls.Should().BeEmpty();
    }

    /// <summary>An admin-area call acts for no client's hosting and is refused.</summary>
    [Fact]
    public async Task AdminCall_IsNotAllowedAsync()
    {
        var owner = await fixture.OwnerWithServiceAsync("plugin-ssh-admin");
        await fixture.BindProductsOfAsync(fixture.SshPluginId, owner.ServiceId);

        var failure = await RefusalAsync(PluginCallBinding.WithoutClient(fixture.SshPluginId), owner.ServiceId);

        failure.Should().Be(PluginHostAccessFailure.NotAllowed);
        fixture.Agents().AgentFor(owner.ServerId).Calls.Should().BeEmpty();
    }

    /// <summary>Another client's service answers NotFound, as a missing one does, even when its product is bound.</summary>
    [Fact]
    public async Task AnotherClientsService_IsNotFoundAsync()
    {
        var owner = await fixture.OwnerWithServiceAsync("plugin-ssh-owner");
        var stranger = await fixture.OwnerWithServiceAsync("plugin-ssh-stranger");
        await fixture.BindProductsOfAsync(fixture.SshPluginId, owner.ServiceId);

        var foreign = await RefusalAsync(PluginSshFixture.ClientCall(fixture.SshPluginId, stranger.ClientId), owner.ServiceId);
        var missing = await RefusalAsync(PluginSshFixture.ClientCall(fixture.SshPluginId, owner.ClientId), int.MaxValue);

        foreign.Should().Be(PluginHostAccessFailure.NotFound);
        missing.Should().Be(PluginHostAccessFailure.NotFound);
        fixture.Agents().AgentFor(owner.ServerId).Calls.Should().BeEmpty();
    }

    /// <summary>A service whose product is not bound to the plugin is refused.</summary>
    [Fact]
    public async Task UnboundProduct_IsNotAllowedAsync()
    {
        var owner = await fixture.OwnerWithServiceAsync("plugin-ssh-unbound");

        var failure = await RefusalAsync(PluginSshFixture.ClientCall(fixture.SshPluginId, owner.ClientId), owner.ServiceId);

        failure.Should().Be(PluginHostAccessFailure.NotAllowed);
        fixture.Agents().AgentFor(owner.ServerId).Calls.Should().BeEmpty();
    }

    /// <summary>
    /// A product listed only in the plugin's own manifest is not bound: until an operator binds it, the plugin reaches no
    /// hosting, however many product ids it claims.
    /// </summary>
    [Fact]
    public async Task ProductListedOnlyInTheManifest_IsNotAllowedAsync()
    {
        var owner = await fixture.OwnerWithServiceAsync("plugin-ssh-greedy");
        (await fixture.ProductOfAsync(owner.ServiceId)).Should().BeLessThanOrEqualTo(PluginSshFixture.GreedyProductIdCeiling);

        var failure = await RefusalAsync(PluginSshFixture.ClientCall(fixture.GreedySshPluginId, owner.ClientId), owner.ServiceId);

        failure.Should().Be(PluginHostAccessFailure.NotAllowed);
        fixture.Agents().AgentFor(owner.ServerId).Calls.Should().BeEmpty();
    }

    /// <summary>
    /// A plugin switched off while its key is being installed gets no key: the issue answers NotAllowed, and the key that
    /// reached the account is taken back and recorded as revoked, so it does not outlive the plugin.
    /// </summary>
    [Fact]
    public async Task PluginSwitchedOffDuringTheInstall_GetsNoKeyAndTheKeyIsRevokedAsync()
    {
        var owner = await fixture.OwnerWithServiceAsync("plugin-ssh-switched-off");
        await fixture.BindProductsOfAsync(fixture.SshPluginId, owner.ServiceId);
        var agent = fixture.Agents().AgentFor(owner.ServerId);
        agent.WhileAdding = () => fixture.SwitchAsync(fixture.SshPluginId, enabled: false);
        try
        {
            var failure = await RefusalAsync(PluginSshFixture.ClientCall(fixture.SshPluginId, owner.ClientId), owner.ServiceId);

            failure.Should().Be(PluginHostAccessFailure.NotAllowed);
            agent.KeysOf("web").Should().BeEmpty();
            (await fixture.KeysOfAsync(owner.ServiceId)).Should().ContainSingle()
                .Which.SyncState.Should().Be(SshKeySyncState.Revoked);
        }
        finally
        {
            agent.WhileAdding = null;
            await fixture.SwitchAsync(fixture.SshPluginId, enabled: true);
        }
    }

    /// <summary>A plugin switched off after the call began, before a key pair is made, is refused and nothing is installed.</summary>
    [Fact]
    public async Task PluginSwitchedOffBeforeTheKeyIsMade_IsNotAllowedAsync()
    {
        var owner = await fixture.OwnerWithServiceAsync("plugin-ssh-off-before");
        await fixture.BindProductsOfAsync(fixture.SshPluginId, owner.ServiceId);
        await fixture.SwitchAsync(fixture.SshPluginId, enabled: false);
        try
        {
            var failure = await RefusalAsync(PluginSshFixture.ClientCall(fixture.SshPluginId, owner.ClientId), owner.ServiceId);

            failure.Should().Be(PluginHostAccessFailure.NotAllowed);
            fixture.Agents().AgentFor(owner.ServerId).Calls.Should().BeEmpty();
            (await fixture.KeysOfAsync(owner.ServiceId)).Should().BeEmpty();
        }
        finally
        {
            await fixture.SwitchAsync(fixture.SshPluginId, enabled: true);
        }
    }

    /// <summary>An unreachable agent answers ServerUnavailable and the record says the install failed.</summary>
    [Fact]
    public async Task UnreachableServer_IsServerUnavailableAsync()
    {
        var owner = await fixture.OwnerWithServiceAsync("plugin-ssh-down");
        await fixture.BindProductsOfAsync(fixture.SshPluginId, owner.ServiceId);
        fixture.Agents().AgentFor(owner.ServerId).FailWith = SshProviderFailure.Unreachable;

        var failure = await RefusalAsync(PluginSshFixture.ClientCall(fixture.SshPluginId, owner.ClientId), owner.ServiceId);

        failure.Should().Be(PluginHostAccessFailure.ServerUnavailable);
        using var scope = fixture.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.SshKeys.SingleAsync(k => k.ClientServiceId == owner.ServiceId)).SyncState
            .Should().Be(SshKeySyncState.InstallFailed);
    }

    /// <summary>A service already holding as many keys as the host allows is refused before a key is generated or sent.</summary>
    [Fact]
    public async Task ServiceAtTheKeyLimit_IsLimitReachedAsync()
    {
        var owner = await fixture.OwnerWithServiceAsync("plugin-ssh-limit");
        await fixture.BindProductsOfAsync(fixture.SshPluginId, owner.ServiceId);
        using var http = AppPluginFixture.WithBearerOf(fixture.Host, owner.Http);
        for (var seed = 1; seed <= SshLimits.MaxKeysPerService; seed++)
        {
            await SshTestSeeds.AddKeyAsync(http, owner.ServiceId, (byte)seed);
        }

        var callsBefore = fixture.Agents().AgentFor(owner.ServerId).Calls.Count;

        var failure = await RefusalAsync(PluginSshFixture.ClientCall(fixture.SshPluginId, owner.ClientId), owner.ServiceId);

        failure.Should().Be(PluginHostAccessFailure.LimitReached);
        fixture.Agents().AgentFor(owner.ServerId).Calls.Count.Should().Be(callsBefore);
        (await fixture.KeysOfAsync(owner.ServiceId)).Should().HaveCount(SshLimits.MaxKeysPerService)
            .And.OnlyContain(key => key.IssuedToPluginId == null);
    }

    /// <summary>A server without SSH key management is refused Unavailable and no agent is called.</summary>
    [Fact]
    public async Task SshNotAvailable_IsUnavailableAsync()
    {
        var owner = await fixture.OwnerOnSshDisabledServerAsync("plugin-ssh-disabled");
        await fixture.BindProductsOfAsync(fixture.SshPluginId, owner.ServiceId);

        var failure = await RefusalAsync(PluginSshFixture.ClientCall(fixture.SshPluginId, owner.ClientId), owner.ServiceId);

        failure.Should().Be(PluginHostAccessFailure.Unavailable);
        fixture.Agents().AgentFor(owner.ServerId).Calls.Should().BeEmpty();
        (await fixture.KeysOfAsync(owner.ServiceId)).Should().BeEmpty();
    }

    /// <summary>An agent that refuses the account outright is answered Unavailable and leaves no key record.</summary>
    /// <param name="agentFailure">The refusal the agent answers with.</param>
    [Theory]
    [InlineData(SshProviderFailure.SshDisabled)]
    [InlineData(SshProviderFailure.AccountSuspended)]
    [InlineData(SshProviderFailure.AccountNotEligible)]
    public async Task AgentRefusal_IsUnavailableAndLeavesNoRecordAsync(SshProviderFailure agentFailure)
    {
        var owner = await fixture.OwnerWithServiceAsync($"plugin-ssh-refused-{agentFailure}".ToLowerInvariant());
        await fixture.BindProductsOfAsync(fixture.SshPluginId, owner.ServiceId);
        fixture.Agents().AgentFor(owner.ServerId).FailWith = agentFailure;

        var failure = await RefusalAsync(PluginSshFixture.ClientCall(fixture.SshPluginId, owner.ClientId), owner.ServiceId);

        failure.Should().Be(PluginHostAccessFailure.Unavailable);
        (await fixture.KeysOfAsync(owner.ServiceId)).Should().BeEmpty();
    }

    /// <summary>Two issues for one service are two identities, each recorded as the plugin's.</summary>
    [Fact]
    public async Task EachIssue_IsANewIdentityAsync()
    {
        var owner = await fixture.OwnerWithServiceAsync("plugin-ssh-twice");
        await fixture.BindProductsOfAsync(fixture.SshPluginId, owner.ServiceId);
        var call = PluginSshFixture.ClientCall(fixture.SshPluginId, owner.ClientId);

        var first = await fixture.AsPluginAsync(call, api => api.IssueSshAccessAsync(owner.ServiceId, "first", CancellationToken.None));
        var second = await fixture.AsPluginAsync(call, api => api.IssueSshAccessAsync(owner.ServiceId, "second", CancellationToken.None));

        second.KeyId.Should().NotBe(first.KeyId);
        second.PrivateKey.Should().NotBe(first.PrivateKey);
        (await fixture.KeysOfAsync(owner.ServiceId)).Should().HaveCount(2)
            .And.OnlyContain(key => key.IssuedToPluginId == fixture.SshPluginId);
    }

    /// <summary>
    /// A contact without <see cref="ClientPermission.ViewModifyPasswords"/> may not have hosting credentials issued through
    /// a plugin, as they may not manage the account's access in the portal: NotAllowed, and no agent is called.
    /// </summary>
    [Fact]
    public async Task ContactWithoutThePasswordsRight_IsNotAllowedAsync()
    {
        var owner = await fixture.OwnerWithServiceAsync("plugin-ssh-contact");
        await fixture.BindProductsOfAsync(fixture.SshPluginId, owner.ServiceId);
        var contact = PluginSshFixture.ClientCall(
            fixture.SshPluginId, owner.ClientId, ClientPermission.All & ~ClientPermission.ViewModifyPasswords);

        var failure = await RefusalAsync(contact, owner.ServiceId);

        failure.Should().Be(PluginHostAccessFailure.NotAllowed);
        fixture.Agents().AgentFor(owner.ServerId).Calls.Should().BeEmpty();
        (await fixture.KeysOfAsync(owner.ServiceId)).Should().BeEmpty();
    }

    /// <summary>
    /// A limiter that cannot answer refuses (fail closed, security.md): the issue does not go ahead, nothing is generated,
    /// stored or sent. The limiter is disposed, the one way a real limiter stops answering in-process; the port does not
    /// catch that, so the plugin sees an exception (502 <c>PLUGIN_FAILED</c> at the door), never an issued key.
    /// </summary>
    [Fact]
    public async Task ALimiterThatCannotAnswer_RefusesTheIssueAsync()
    {
        var owner = await fixture.OwnerWithServiceAsync("plugin-ssh-budget-down");
        await fixture.BindProductsOfAsync(fixture.SshPluginId, owner.ServiceId);
        var limiter = new PluginRateLimiter(
            new RateLimitOptions { SshKeys = new RateLimitTierOptions { PermitLimit = 100, WindowSeconds = 60 } }, disabled: false);
        limiter.Dispose();
        using var host = fixture.Host.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<PluginRateLimiter>();
            services.AddSingleton(limiter);
        }));
        var binding = PluginSshFixture.ClientCall(fixture.SshPluginId, owner.ClientId);

        await PluginSshFixture.AsPluginAsync(host, binding, async api =>
        {
            var act = () => api.IssueSshAccessAsync(owner.ServiceId, Label, CancellationToken.None);
            await act.Should().ThrowAsync<ObjectDisposedException>();
            return true;
        });

        fixture.Agents().AgentFor(owner.ServerId).Calls.Should().BeEmpty();
        (await fixture.KeysOfAsync(owner.ServiceId)).Should().BeEmpty();
    }

    /// <summary>
    /// Once the plugin's SSH issue budget for the client is spent, the next issue is refused as ServerUnavailable before a
    /// key pair is generated: the agent sees one add only.
    /// </summary>
    [Fact]
    public async Task SpentIssueBudget_IsRefusedBeforeAnyKeyIsMadeAsync()
    {
        var owner = await fixture.OwnerWithServiceAsync("plugin-ssh-budget");
        await fixture.BindProductsOfAsync(fixture.SshPluginId, owner.ServiceId);
        using var host = fixture.Host.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<PluginRateLimiter>();
            services.AddSingleton(new PluginRateLimiter(
                new RateLimitOptions
                {
                    PluginDefault = new RateLimitTierOptions { PermitLimit = 100, WindowSeconds = 60 },
                    PluginStrict = new RateLimitTierOptions { PermitLimit = 100, WindowSeconds = 60 },
                    PluginHeavy = new RateLimitTierOptions { PermitLimit = 100, WindowSeconds = 60 },
                    PluginStreamPermitLimit = 1,
                    SshKeys = new RateLimitTierOptions { PermitLimit = 1, WindowSeconds = 60 },
                },
                disabled: false));
        }));
        var binding = PluginSshFixture.ClientCall(fixture.SshPluginId, owner.ClientId);

        using (var first = host.Services.CreateScope())
        {
            await first.ServiceProvider.GetRequiredService<IPluginCallServices>().Create(binding).Host
                .IssueSshAccessAsync(owner.ServiceId, Label, CancellationToken.None);
        }

        using var second = host.Services.CreateScope();
        var api = second.ServiceProvider.GetRequiredService<IPluginCallServices>().Create(binding).Host;
        var act = () => api.IssueSshAccessAsync(owner.ServiceId, Label, CancellationToken.None);

        (await act.Should().ThrowAsync<PluginHostAccessException>()).Which.Failure
            .Should().Be(PluginHostAccessFailure.ServerUnavailable);

        // The derived host has its own fake agents, so the one add is looked for there.
        var agents = (FakeSshKeyProviderFactory)host.Services.GetRequiredService<ISshKeyProviderFactory>();
        agents.AgentFor(owner.ServerId).KeysOf("web").Should().ContainSingle();
    }

    /// <summary>The raw bytes of the base64 body of a PEM private key.</summary>
    /// <param name="privateKey">The OpenSSH private key text.</param>
    /// <returns>The decoded bytes.</returns>
    private static byte[] DecodedBody(string privateKey)
    {
        return Convert.FromBase64String(string.Concat(
            privateKey.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => !line.StartsWith("-----", StringComparison.Ordinal))));
    }

    /// <summary>Calls issue and returns the refusal the plugin catches.</summary>
    /// <param name="binding">The call.</param>
    /// <param name="serviceId">The service asked about.</param>
    /// <returns>The failure the host reported.</returns>
    private async Task<PluginHostAccessFailure> RefusalAsync(PluginCallBinding binding, int serviceId)
    {
        var refused = await fixture.AsPluginAsync(binding, async api =>
        {
            var act = () => api.IssueSshAccessAsync(serviceId, Label, CancellationToken.None);
            return (await act.Should().ThrowAsync<PluginHostAccessException>()).Which;
        });
        return refused.Failure;
    }
}
