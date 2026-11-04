using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.RevokePluginIssuedSshKeys;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Domain.Plugins.Events;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Removing a plugin revokes every live SSH key issued to it (<see cref="RevokePluginIssuedSshKeysCommand"/>, dispatched
/// on <see cref="PluginRemovedEvent"/>): the plugin's keys leave the account, the client's own keys stay, a key the agent
/// does not confirm is recorded as a failed revoke and taken off on the next delivery, another plugin's key is left
/// alone, and a repeat delivery changes nothing.
/// </summary>
/// <param name="fixture">The host with the SSH plugin.</param>
public sealed class RevokePluginIssuedSshKeysTests(PluginSshFixture fixture) : IClassFixture<PluginSshFixture>
{
    /// <summary>The removal event revokes the plugin's key, audits it and leaves the client's own key in place.</summary>
    [Fact]
    public async Task PluginRemoved_RevokesOnlyThePluginsKeysAsync()
    {
        var owner = await fixture.OwnerWithServiceAsync("removed-plugin-keys");
        await fixture.BindProductsOfAsync(fixture.SshPluginId, owner.ServiceId);
        using var http = AppPluginFixture.WithBearerOf(fixture.Host, owner.Http);
        var clientKey = await SshTestSeeds.AddKeyAsync(http, owner.ServiceId, 9);
        var issued = await fixture.AsPluginAsync(
            PluginSshFixture.ClientCall(fixture.SshPluginId, owner.ClientId),
            api => api.IssueSshAccessAsync(owner.ServiceId, "deploy", CancellationToken.None));

        await fixture.Host.Services.PublishAndWaitAsync(new PluginRemovedEvent(fixture.SshPluginId, DateTimeOffset.UtcNow));

        using var scope = fixture.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await StateOfAsync(context, issued.KeyId)).Should().Be(SshKeySyncState.Revoked);
        (await StateOfAsync(context, clientKey)).Should().Be(SshKeySyncState.Installed);
        fixture.Agents().AgentFor(owner.ServerId).KeysOf("web").Should().ContainSingle();
        (await context.ActivityLogs.AnyAsync(l => l.ClientId == owner.ClientId
            && l.Description.Contains($"issued to removed plugin {fixture.SshPluginId}"))).Should().BeTrue();
    }

    /// <summary>An unreachable agent leaves the key live as a failed revoke; the next delivery takes it off.</summary>
    [Fact]
    public async Task UnconfirmedRevoke_IsRecordedAndRetriedAsync()
    {
        var owner = await fixture.OwnerWithServiceAsync("removed-plugin-retry");
        await fixture.BindProductsOfAsync(fixture.SshPluginId, owner.ServiceId);
        var issued = await fixture.AsPluginAsync(
            PluginSshFixture.ClientCall(fixture.SshPluginId, owner.ClientId),
            api => api.IssueSshAccessAsync(owner.ServiceId, "deploy", CancellationToken.None));
        var agent = fixture.Agents().AgentFor(owner.ServerId);
        agent.FailWith = SshProviderFailure.Unreachable;

        var first = await RunAsync(fixture.SshPluginId);
        agent.FailWith = null;
        var second = await RunAsync(fixture.SshPluginId);

        first.Failed.Should().Be(1);
        second.Should().Be(new PluginSshKeyRevocationDto(1, 0));
        using var scope = fixture.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await StateOfAsync(context, issued.KeyId)).Should().Be(SshKeySyncState.Revoked);
    }

    /// <summary>A key issued to another plugin on the same service is left live and on the account.</summary>
    [Fact]
    public async Task AnotherPluginsKey_IsLeftAloneAsync()
    {
        var owner = await fixture.OwnerWithServiceAsync("removed-plugin-other");
        Guid otherKeyId;
        using (var seed = fixture.Host.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var key = SshKey.Create(
                owner.ServiceId, owner.ServerId, "web", "other deploy", SshTestSeeds.Key(77),
                $"SHA256:other-{Guid.NewGuid():N}", issuedToPluginId: "other-plugin");
            key.MarkInstalled();
            db.SshKeys.Add(key);
            await db.SaveChangesAsync(CancellationToken.None);
            otherKeyId = key.KeyId;
        }

        var result = await RunAsync(fixture.SshPluginId);

        result.Should().Be(new PluginSshKeyRevocationDto(0, 0));
        using var scope = fixture.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await StateOfAsync(context, otherKeyId)).Should().Be(SshKeySyncState.Installed);
    }

    /// <summary>A second delivery after a successful revoke finds nothing live and changes nothing (idempotent).</summary>
    [Fact]
    public async Task SecondDelivery_ChangesNothingAsync()
    {
        var owner = await fixture.OwnerWithServiceAsync("removed-plugin-twice");
        await fixture.BindProductsOfAsync(fixture.SshPluginId, owner.ServiceId);
        var issued = await fixture.AsPluginAsync(
            PluginSshFixture.ClientCall(fixture.SshPluginId, owner.ClientId),
            api => api.IssueSshAccessAsync(owner.ServiceId, "deploy", CancellationToken.None));
        (await RunAsync(fixture.SshPluginId)).Revoked.Should().BeGreaterThanOrEqualTo(1);
        var callsAfterFirst = fixture.Agents().AgentFor(owner.ServerId).Calls.Count;
        int linesAfterFirst;
        using (var before = fixture.Host.Services.CreateScope())
        {
            linesAfterFirst = await before.ServiceProvider.GetRequiredService<IHostpanelDbContext>().ActivityLogs
                .CountAsync(l => l.ClientId == owner.ClientId);
        }

        var second = await RunAsync(fixture.SshPluginId);

        second.Should().Be(new PluginSshKeyRevocationDto(0, 0));
        fixture.Agents().AgentFor(owner.ServerId).Calls.Count.Should().Be(callsAfterFirst);
        using var scope = fixture.Host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await StateOfAsync(context, issued.KeyId)).Should().Be(SshKeySyncState.Revoked);
        (await context.ActivityLogs.CountAsync(l => l.ClientId == owner.ClientId)).Should().Be(linesAfterFirst);
    }

    /// <summary>Dispatches the command once, as one delivery of the removal would.</summary>
    /// <param name="pluginId">The removed plugin.</param>
    /// <returns>What the delivery did.</returns>
    private async Task<PluginSshKeyRevocationDto> RunAsync(string pluginId)
    {
        using var scope = fixture.Host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var outcome = await bus.InvokeAsync<Result<PluginSshKeyRevocationDto>>(
            new RevokePluginIssuedSshKeysCommand(pluginId), CancellationToken.None);
        return outcome.Value!;
    }

    /// <summary>Reads a key's state.</summary>
    /// <param name="context">The data context.</param>
    /// <param name="keyId">The key.</param>
    /// <returns>Its sync state.</returns>
    private static Task<SshKeySyncState> StateOfAsync(IHostpanelDbContext context, Guid keyId)
    {
        return context.SshKeys.Where(k => k.KeyId == keyId).Select(k => k.SyncState).SingleAsync();
    }
}
