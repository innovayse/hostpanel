using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Provisioning.Events;
using Innovayse.Hostpanel.Domain.Services.Events;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Provisioning;

/// <summary>
/// Tests for <c>ServiceUnsuspendedHandler</c>: an unsuspended service unsuspends the hosting account via the provisioning provider.
/// Each test builds its own host from <see cref="IntegrationTestFactory"/> with the one port this platform
/// does not own (the hosting server's control-panel API) replaced by <see cref="RecordingProvisioningProvider"/>,
/// publishes the event exactly once through the real message bus and asserts the provider call happened exactly once.
/// Once the panel has unlocked the account, the handler also puts the service's suspended SSH keys back, because an
/// agent refuses keys for a locked account; a panel that did not answer <c>Updated</c> leaves them suspended.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ServiceUnsuspendedHandlerTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An unsuspended service calls the provisioning provider's unsuspend endpoint exactly once.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ServiceUnsuspended_CallsProviderUnsuspendExactlyOnceAsync()
    {
        var provider = new RecordingProvisioningProvider();
        var host = ProvisioningTestSeeds.WithRecordingProvider(factory, provider);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var (clientId, _, serviceId) = await ProvisioningTestSeeds.SeedActiveServiceAsync(context);

        await scope.ServiceProvider.PublishAndWaitAsync(new ServiceUnsuspendedEvent(serviceId, clientId, TestMoments.Seeded));

        var reference = Assert.Single(provider.Unsuspended);
        Assert.Equal("provisioning-ref-1", reference);
    }

    /// <summary>
    /// An unsuspension delivered late, after the service was suspended again (a redelivery after a restart,
    /// or a retry behind the new suspension), leaves the panel alone: the account the platform shows as
    /// suspended is not unlocked.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ServiceUnsuspended_DeliveredAfterTheServiceIsSuspendedAgain_CallsNothingAsync()
    {
        var provider = new RecordingProvisioningProvider();
        var host = ProvisioningTestSeeds.WithRecordingProvider(factory, provider);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var (clientId, _, serviceId) = await ProvisioningTestSeeds.SeedActiveServiceAsync(context);
        using (scope.ServiceProvider.GetRequiredService<IDomainEventGate>().Suppress())
        {
            var service = await context.ClientServices.FindAsync([serviceId], CancellationToken.None);
            service!.Suspend(DateTimeOffset.UtcNow, hostingAlreadyUpdated: true);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        await scope.ServiceProvider.PublishAndWaitAsync(new ServiceUnsuspendedEvent(serviceId, clientId, TestMoments.Seeded));

        Assert.Empty(provider.Unsuspended);
    }

    /// <summary>
    /// When the panel answers <c>Updated</c>, the handler restores the service's suspended SSH keys on the
    /// server they were issued on, under the same ids.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ServiceUnsuspended_WhenThePanelUpdated_RestoresTheSuspendedSshKeysAsync()
    {
        var (http, clientId, serverId, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "unsuspend-ssh-updated");
        await SshTestSeeds.AddKeyAsync(http, serviceId, 1);
        await SshTestSeeds.AddKeyAsync(http, serviceId, 2);
        var agent = SshTestSeeds.Agents(factory).AgentFor(serverId);
        var installed = agent.KeysOf("web").Keys.ToList();
        await SuspendKeysThenUnsuspendQuietlyAsync(clientId, serviceId);
        agent.KeysOf("web").Should().BeEmpty();

        await PublishAsync(factory.Services, new ServiceUnsuspendedEvent(serviceId, clientId, TestMoments.Seeded));

        agent.KeysOf("web").Keys.Should().BeEquivalentTo(installed, "the same keys come back, under the same ids");
        (await StatesAsync(serviceId)).Should().Equal(SshKeySyncState.Installed, SshKeySyncState.Installed);
    }

    /// <summary>
    /// When the panel fails to unlock the account, the handler does not ask for the key restore, so the
    /// service's SSH keys stay suspended and nothing is installed on the server.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ServiceUnsuspended_WhenThePanelFailed_LeavesTheSshKeysSuspendedAsync()
    {
        var (http, clientId, serverId, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "unsuspend-ssh-failed");
        await SshTestSeeds.AddKeyAsync(http, serviceId, 1);
        await SuspendKeysThenUnsuspendQuietlyAsync(clientId, serviceId);
        await using var host = ProvisioningTestSeeds.HostWithFakeProvider(factory, new FakeProvisioningProvider { ShouldFail = true });

        await PublishAsync(host.Services, new ServiceUnsuspendedEvent(serviceId, clientId, TestMoments.Seeded));

        var agents = (FakeSshKeyProviderFactory)host.Services.GetRequiredService<ISshKeyProviderFactory>();
        agents.AgentFor(serverId).KeysOf("web").Should().BeEmpty("no restore was sent to the server");
        (await StatesAsync(serviceId)).Should().Equal(SshKeySyncState.Suspended);
    }

    /// <summary>
    /// Suspends the service and takes its keys off through the service's own suspension event, then
    /// marks the service active again with events suppressed, so the only restore left to happen is
    /// the one the provisioning handler under test dispatches.
    /// </summary>
    /// <param name="clientId">The owning client.</param>
    /// <param name="serviceId">The service.</param>
    /// <returns>A task that completes once the keys are suspended and the service is active.</returns>
    private async Task SuspendKeysThenUnsuspendQuietlyAsync(int clientId, int serviceId)
    {
        await SshTestSeeds.ChangeServiceQuietlyAsync(factory, serviceId, s => s.Suspend(DateTimeOffset.UtcNow, hostingAlreadyUpdated: true));
        await PublishAsync(factory.Services, new ClientServiceSuspendedEvent(serviceId, clientId, TestMoments.Seeded));
        (await StatesAsync(serviceId)).Should().OnlyContain(state => state == SshKeySyncState.Suspended);
        await SshTestSeeds.ChangeServiceQuietlyAsync(factory, serviceId, s => s.Unsuspend(DateTimeOffset.UtcNow, hostingAlreadyUpdated: true));
    }

    /// <summary>Publishes an event through the real bus of the given host and waits until it and every message it cascades to were handled.</summary>
    /// <param name="services">The host's root service provider.</param>
    /// <param name="evt">The event.</param>
    /// <returns>A task that completes once every handler of the event has run.</returns>
    private static async Task PublishAsync(IServiceProvider services, object evt)
    {
        await services.PublishAndWaitAsync(evt);
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
