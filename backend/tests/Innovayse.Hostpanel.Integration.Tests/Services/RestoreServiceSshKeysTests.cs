using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.RestoreServiceSshKeys;
using Innovayse.Hostpanel.Application.Services.Commands.SuspendServiceSshKeys;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// <see cref="RestoreServiceSshKeysCommand"/>: an unsuspended service's keys go back on the server
/// they were issued on, under the same ids.
/// </summary>
/// <param name="factory">The shared API and PostgreSQL host.</param>
public sealed class RestoreServiceSshKeysTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The keys taken off at suspension come back under the same ids.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Unsuspension_PutsTheSameKeysBackAsync()
    {
        var (_, _, serverId, serviceId) = await SuspendedWithKeysAsync("ssh-restore", 2);
        var agent = SshTestSeeds.Agents(factory).AgentFor(serverId);
        await SshTestSeeds.ChangeServiceQuietlyAsync(factory, serviceId, s => s.Unsuspend(TestMoments.Seeded, hostingAlreadyUpdated: true));

        var outcome = await InvokeAsync(new RestoreServiceSshKeysCommand(serviceId));

        outcome.IsSuccess.Should().BeTrue();
        var keys = await KeysAsync(serviceId);
        keys.Select(k => k.SyncState).Should().Equal(SshKeySyncState.Installed, SshKeySyncState.Installed);
        agent.KeysOf("web").Keys.Should().BeEquivalentTo(keys.Select(k => k.KeyId), "the same keys come back, under the same ids");
    }

    /// <summary>A key limited to some sources comes back with the same sources, never open to every address.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Unsuspension_PutsAKeyBackWithTheSourcesItWasIssuedWithAsync()
    {
        string[] sources = ["203.0.113.7", "198.51.100.0/24"];
        var scenario = await SshTestSeeds.OwnerWithServiceAsync(factory, "ssh-restore-from");
        var keyId = await SeedInstalledKeyAsync(scenario, sources);
        await SshTestSeeds.ChangeServiceQuietlyAsync(
            factory, scenario.ServiceId, s => s.Suspend(TestMoments.Seeded, hostingAlreadyUpdated: true));
        (await InvokeAsync(new SuspendServiceSshKeysCommand(scenario.ServiceId))).IsSuccess.Should().BeTrue();
        await SshTestSeeds.ChangeServiceQuietlyAsync(
            factory, scenario.ServiceId, s => s.Unsuspend(TestMoments.Seeded, hostingAlreadyUpdated: true));
        var agent = SshTestSeeds.Agents(factory).AgentFor(scenario.ServerId);

        (await InvokeAsync(new RestoreServiceSshKeysCommand(scenario.ServiceId))).IsSuccess.Should().BeTrue();

        (await KeysAsync(scenario.ServiceId)).Single().SyncState.Should().Be(SshKeySyncState.Installed);
        agent.AllowedFromOf(keyId).Should().Equal(sources);
    }

    /// <summary>An unconfirmed restore is a failed install, with the reason.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Unsuspension_WithTheServerDown_LeavesTheKeyAsAFailedInstallAsync()
    {
        var (_, _, serverId, serviceId) = await SuspendedWithKeysAsync("ssh-restore-down", 1);
        await SshTestSeeds.ChangeServiceQuietlyAsync(factory, serviceId, s => s.Unsuspend(TestMoments.Seeded, hostingAlreadyUpdated: true));
        SshTestSeeds.Agents(factory).AgentFor(serverId).FailWith = SshProviderFailure.Unreachable;

        await InvokeAsync(new RestoreServiceSshKeysCommand(serviceId));

        var key = (await KeysAsync(serviceId)).Single();
        key.SyncState.Should().Be(SshKeySyncState.InstallFailed);
        key.LastSyncError.Should().Be(SshProviderFailure.Unreachable);
    }

    /// <summary>
    /// An account the panel has not unlocked yet keeps the key suspended, a wait rather than a failure,
    /// and the second pass sent once the panel unlocked it restores the key.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task StillLockedAccount_KeepsTheKeySuspended_AndTheSecondPassRestoresItAsync()
    {
        var (_, _, serverId, serviceId) = await SuspendedWithKeysAsync("ssh-restore-race", 1);
        await SshTestSeeds.ChangeServiceQuietlyAsync(factory, serviceId, s => s.Unsuspend(TestMoments.Seeded, hostingAlreadyUpdated: true));
        var agent = SshTestSeeds.Agents(factory).AgentFor(serverId);
        agent.FailWith = SshProviderFailure.AccountSuspended;

        await InvokeAsync(new RestoreServiceSshKeysCommand(serviceId));

        (await KeysAsync(serviceId)).Single().SyncState.Should().Be(SshKeySyncState.Suspended);

        agent.FailWith = null;
        await InvokeAsync(new RestoreServiceSshKeysCommand(serviceId));

        (await KeysAsync(serviceId)).Single().SyncState.Should().Be(SshKeySyncState.Installed);
        agent.KeysOf("web").Should().HaveCount(1);
    }

    /// <summary>A service that is still suspended is refused and its keys stay off the server.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SuspendedService_IsRefusedAsync()
    {
        var (_, _, serverId, serviceId) = await SuspendedWithKeysAsync("ssh-restore-early", 1);

        var outcome = await InvokeAsync(new RestoreServiceSshKeysCommand(serviceId));

        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_NOT_ACTIVE);
        SshTestSeeds.Agents(factory).AgentFor(serverId).KeysOf("web").Should().BeEmpty();
    }

    /// <summary>An unknown service is refused with <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UnknownService_IsRefusedAsync()
    {
        var outcome = await InvokeAsync(new RestoreServiceSshKeysCommand(ServicesTestSeeds.NoSuchServiceId));

        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>Seeds an owner with a service holding keys, then suspends it and takes the keys off.</summary>
    /// <param name="label">Label for the generated owner.</param>
    /// <param name="keyCount">How many keys to add.</param>
    /// <returns>The scenario.</returns>
    private async Task<SshOwnerScenario> SuspendedWithKeysAsync(string label, byte keyCount)
    {
        var scenario = await SshTestSeeds.OwnerWithServiceAsync(factory, label);
        for (byte seed = 1; seed <= keyCount; seed++)
        {
            await SshTestSeeds.AddKeyAsync(scenario.Http, scenario.ServiceId, seed);
        }

        await SshTestSeeds.ChangeServiceQuietlyAsync(
            factory, scenario.ServiceId, s => s.Suspend(TestMoments.Seeded, hostingAlreadyUpdated: true));
        (await InvokeAsync(new SuspendServiceSshKeysCommand(scenario.ServiceId))).IsSuccess.Should().BeTrue();
        return scenario;
    }

    /// <summary>Records an installed key with sources on the scenario's service, as a plugin's issue would have left it.</summary>
    /// <param name="scenario">The owner and service.</param>
    /// <param name="sources">The sources the key was issued with.</param>
    /// <returns>The key's public id.</returns>
    private async Task<Guid> SeedInstalledKeyAsync(SshOwnerScenario scenario, string[] sources)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var key = SshKey.Create(
            scenario.ServiceId, scenario.ServerId, "web", "deploy", "ssh-ed25519 AAAA", $"SHA256:{Guid.NewGuid():N}",
            issuedToPluginId: null, allowedFrom: sources);
        key.MarkInstalled();
        context.SshKeys.Add(key);
        await context.SaveChangesAsync(CancellationToken.None);
        return key.KeyId;
    }

    /// <summary>Dispatches a command through the real bus, in a fresh scope.</summary>
    /// <param name="command">The command.</param>
    /// <returns>The outcome.</returns>
    private async Task<Result> InvokeAsync(object command)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync<Result>(command, CancellationToken.None);
    }

    /// <summary>The service's keys, oldest first, untracked.</summary>
    /// <param name="serviceId">The service.</param>
    /// <returns>The keys.</returns>
    private async Task<List<SshKey>> KeysAsync(int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().SshKeys.AsNoTracking()
            .Where(k => k.ClientServiceId == serviceId).OrderBy(k => k.Id).ToListAsync();
    }
}
