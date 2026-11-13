using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Events;
using Innovayse.Hostpanel.Domain.Services.Events;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// The service's own suspension events drive its SSH keys: <see cref="SuspendSshKeysOnServiceSuspendedHandler"/>
/// takes them off and <see cref="RestoreSshKeysOnServiceUnsuspendedHandler"/> puts them back. Control
/// panels suspend by locking the password, which does not stop a key from logging in.
/// </summary>
/// <param name="factory">The shared API and PostgreSQL host.</param>
public sealed class SshKeysFollowServiceSuspensionTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Suspension takes the service's keys off the server and unsuspension puts the same ids back.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task SuspensionEvents_TakeKeysOffAndPutThemBackAsync()
    {
        var (http, clientId, serverId, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "ssh-follow");
        await SshTestSeeds.AddKeyAsync(http, serviceId, 1);
        await SshTestSeeds.AddKeyAsync(http, serviceId, 2);
        var agent = SshTestSeeds.Agents(factory).AgentFor(serverId);
        var installed = agent.KeysOf("web").Keys.ToList();

        await SshTestSeeds.ChangeServiceQuietlyAsync(factory, serviceId, s => s.Suspend(DateTimeOffset.UtcNow, hostingAlreadyUpdated: true));
        await PublishAsync(new ClientServiceSuspendedEvent(serviceId, clientId, TestMoments.Seeded));

        agent.KeysOf("web").Should().BeEmpty();
        (await StatesAsync(serviceId)).Should().Equal(SshKeySyncState.Suspended, SshKeySyncState.Suspended);

        await SshTestSeeds.ChangeServiceQuietlyAsync(factory, serviceId, s => s.Unsuspend(DateTimeOffset.UtcNow, hostingAlreadyUpdated: true));
        await PublishAsync(new ClientServiceUnsuspendedEvent(serviceId, clientId, TestMoments.Seeded));

        agent.KeysOf("web").Keys.Should().BeEquivalentTo(installed, "the same keys come back, under the same ids");
        (await StatesAsync(serviceId)).Should().Equal(SshKeySyncState.Installed, SshKeySyncState.Installed);
    }

    /// <summary>Publishes an event through the real bus and waits until it and every message it cascades to were handled.</summary>
    /// <param name="evt">The event.</param>
    /// <returns>A task that completes once every handler of the event has run.</returns>
    private async Task PublishAsync(object evt)
    {
        await factory.Services.PublishAndWaitAsync(evt);
    }

    /// <summary>The service's key states, oldest first.</summary>
    /// <param name="serviceId">The service.</param>
    /// <returns>The states.</returns>
    private async Task<List<SshKeySyncState>> StatesAsync(int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().SshKeys.AsNoTracking()
            .Where(k => k.ClientServiceId == serviceId).OrderBy(k => k.Id).Select(k => k.SyncState).ToListAsync();
    }
}
