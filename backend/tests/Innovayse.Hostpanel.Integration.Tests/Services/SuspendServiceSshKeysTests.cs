using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.SuspendServiceSshKeys;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// <see cref="SuspendServiceSshKeysCommand"/>: a suspended service's keys come off the server they were
/// issued on, because control panels suspend by locking the password, which does not stop a key.
/// </summary>
/// <param name="factory">The shared API and PostgreSQL host.</param>
public sealed class SuspendServiceSshKeysTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Only this service's keys come off the server, and they are kept as suspended.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Suspension_RemovesOnlyThisServicesKeysFromTheServerAsync()
    {
        var (http, clientId, serverId, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "ssh-susp");
        var other = await SshTestSeeds.SeedServiceAsync(factory, clientId, serverId, "other");
        await SshTestSeeds.AddKeyAsync(http, serviceId, 1);
        await SshTestSeeds.AddKeyAsync(http, serviceId, 2);
        await SshTestSeeds.AddKeyAsync(http, other, 3);
        await SshTestSeeds.ChangeServiceQuietlyAsync(factory, serviceId, s => s.Suspend(TestMoments.Seeded, hostingAlreadyUpdated: true));
        var agent = SshTestSeeds.Agents(factory).AgentFor(serverId);

        var outcome = await InvokeAsync(new SuspendServiceSshKeysCommand(serviceId));

        outcome.IsSuccess.Should().BeTrue();
        agent.KeysOf("web").Should().BeEmpty();
        agent.KeysOf("other").Should().HaveCount(1);
        (await KeysAsync(serviceId)).Select(k => k.SyncState).Should().Equal(SshKeySyncState.Suspended, SshKeySyncState.Suspended);
    }

    /// <summary>An unconfirmed removal leaves the key installed, with the reason.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Suspension_WithTheServerDown_KeepsTheKeyInstalledAndRecordsWhyAsync()
    {
        var (http, _, serverId, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "ssh-susp-down");
        await SshTestSeeds.AddKeyAsync(http, serviceId, 1);
        await SshTestSeeds.ChangeServiceQuietlyAsync(factory, serviceId, s => s.Suspend(TestMoments.Seeded, hostingAlreadyUpdated: true));
        SshTestSeeds.Agents(factory).AgentFor(serverId).FailWith = SshProviderFailure.Unreachable;

        var outcome = await InvokeAsync(new SuspendServiceSshKeysCommand(serviceId));

        // Still on the server as far as anyone knows: claiming otherwise would hide a working login.
        outcome.IsSuccess.Should().BeTrue();
        var key = (await KeysAsync(serviceId)).Single();
        key.SyncState.Should().Be(SshKeySyncState.Installed);
        key.LastSyncError.Should().Be(SshProviderFailure.Unreachable);
    }

    /// <summary>A service that is not suspended is refused and its keys stay on the server.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ActiveService_IsRefusedAndKeepsItsKeysAsync()
    {
        var (http, _, serverId, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "ssh-susp-active");
        await SshTestSeeds.AddKeyAsync(http, serviceId, 1);

        var outcome = await InvokeAsync(new SuspendServiceSshKeysCommand(serviceId));

        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_NOT_SUSPENDED);
        SshTestSeeds.Agents(factory).AgentFor(serverId).KeysOf("web").Should().HaveCount(1);
    }

    /// <summary>An unknown service is refused with <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UnknownService_IsRefusedAsync()
    {
        var outcome = await InvokeAsync(new SuspendServiceSshKeysCommand(ServicesTestSeeds.NoSuchServiceId));

        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
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
