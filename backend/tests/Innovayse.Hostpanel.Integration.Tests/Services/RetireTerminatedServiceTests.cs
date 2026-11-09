using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.RetireTerminatedService;
using Innovayse.Hostpanel.Domain.Servers;
using Innovayse.Hostpanel.Domain.Servers.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Domain.Ssh;
using Innovayse.Hostpanel.Domain.Ssh.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <see cref="RetireTerminatedServiceCommand"/>: a terminated service's open
/// cancellation requests are closed and its SSH keys revoked on the server they were issued on; a
/// running one is refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RetireTerminatedServiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// The open request is closed; the live key, whose server has no agent HostPanel can ask, is left
    /// visible as unconfirmed rather than declared gone; running it again changes nothing.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RetireTerminatedService_ClosesRequestsAndRevokesKeys()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Terminated);
        await SeedRequestAndKeyAsync(serviceId);
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var first = await bus.InvokeAsync<Result>(new RetireTerminatedServiceCommand(serviceId), CancellationToken.None);
        var again = await bus.InvokeAsync<Result>(new RetireTerminatedServiceCommand(serviceId), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        again.IsSuccess.Should().BeTrue();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.CancellationRequests.AsNoTracking().SingleAsync(r => r.ServiceId == serviceId, CancellationToken.None))
            .Status.Should().Be(CancellationStatus.Closed);
        var key = await context.SshKeys.AsNoTracking().SingleAsync(k => k.ClientServiceId == serviceId, CancellationToken.None);
        key.SyncState.Should().Be(SshKeySyncState.RevokeFailed);
        key.LastSyncError.Should().Be(SshProviderFailure.AgentNotConfigured);
    }

    /// <summary>
    /// Every active key is revoked on the server it was issued on, and only that service's keys; a key
    /// the server does not confirm removed stays visible as unconfirmed.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RetireTerminatedService_RevokesKeysOnTheirServer_AndLeavesOtherServicesAlone()
    {
        var (http, clientId, serverId, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "ssh-term");
        var other = await SshTestSeeds.SeedServiceAsync(factory, clientId, serverId, "other");
        await SshTestSeeds.AddKeyAsync(http, serviceId, 1);
        await SshTestSeeds.AddKeyAsync(http, serviceId, 2);
        await SshTestSeeds.AddKeyAsync(http, other, 3);
        await SshTestSeeds.ChangeServiceQuietlyAsync(factory, serviceId, s => s.Terminate(DateTimeOffset.UtcNow, "test", hostingAlreadyUpdated: true));
        var agent = SshTestSeeds.Agents(factory).AgentFor(serverId);
        using var scope = factory.Services.CreateScope();

        var outcome = await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync<Result>(new RetireTerminatedServiceCommand(serviceId), CancellationToken.None);

        outcome.IsSuccess.Should().BeTrue();
        agent.KeysOf("web").Should().BeEmpty();
        agent.KeysOf("other").Should().HaveCount(1);
        (await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().SshKeys.AsNoTracking()
                .Where(k => k.ClientServiceId == serviceId).Select(k => k.SyncState).ToListAsync())
            .Should().OnlyContain(s => s == SshKeySyncState.Revoked);
    }

    /// <summary>With the server down the key is kept as <see cref="SshKeySyncState.RevokeFailed"/>, with the reason.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RetireTerminatedService_WithTheServerDown_KeepsKeysVisibleAsUnconfirmed()
    {
        var (http, _, serverId, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "ssh-term-down");
        await SshTestSeeds.AddKeyAsync(http, serviceId, 1);
        await SshTestSeeds.ChangeServiceQuietlyAsync(factory, serviceId, s => s.Terminate(DateTimeOffset.UtcNow, "test", hostingAlreadyUpdated: true));
        SshTestSeeds.Agents(factory).AgentFor(serverId).FailWith = SshProviderFailure.Unreachable;
        using var scope = factory.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync<Result>(new RetireTerminatedServiceCommand(serviceId), CancellationToken.None);

        var key = await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().SshKeys.AsNoTracking()
            .SingleAsync(k => k.ClientServiceId == serviceId);
        key.SyncState.Should().Be(SshKeySyncState.RevokeFailed);
        key.LastSyncError.Should().Be(SshProviderFailure.Unreachable);
    }

    /// <summary>A service still running is refused and its request and key are left alone.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RetireTerminatedService_RunningServiceIsRefused()
    {
        var serviceId = await ServicesTestSeeds.SeedServiceAsync(factory, ServiceStatus.Active);
        await SeedRequestAndKeyAsync(serviceId);
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var outcome = await bus.InvokeAsync<Result>(new RetireTerminatedServiceCommand(serviceId), CancellationToken.None);

        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_NOT_TERMINATED);
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.CancellationRequests.AsNoTracking().SingleAsync(r => r.ServiceId == serviceId, CancellationToken.None))
            .Status.Should().Be(CancellationStatus.Open);
        (await context.SshKeys.AsNoTracking().SingleAsync(k => k.ClientServiceId == serviceId, CancellationToken.None))
            .SyncState.Should().Be(SshKeySyncState.Installed);
    }

    /// <summary>An unknown service is refused with <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RetireTerminatedService_UnknownServiceRefusesNotFound()
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var outcome = await bus.InvokeAsync<Result>(
            new RetireTerminatedServiceCommand(ServicesTestSeeds.NoSuchServiceId), CancellationToken.None);

        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>
    /// Seeds one open cancellation request and one installed SSH key on the service. The key is issued on
    /// a server of its own that has no SSH settings, so no agent can be asked about it; a fixed server id
    /// could instead be one another test in this class gave an agent (and a failure) to.
    /// </summary>
    /// <param name="serviceId">The service to seed them on.</param>
    /// <returns>A task.</returns>
    private async Task SeedRequestAndKeyAsync(int serviceId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var server = Server.Create(new ServerDetails(
            $"no-agent-{Guid.NewGuid():N}", "no-agent.example.test", null, null, ServerModule.Cwp7, "root", new ServerCredentials(null, null, "hash"), true, null,
            false, false, 0m, null, null, ServerNameServers.None), DateTimeOffset.UtcNow);
        context.Servers.Add(server);
        await context.SaveChangesAsync(CancellationToken.None);

        context.CancellationRequests.Add(CancellationRequest.Create(serviceId, CancellationType.EndOfBillingPeriod, null));
        var key = SshKey.Create(
            serviceId, server.Id, "acct1", "laptop", "ssh-ed25519 AAAA", $"SHA256:{Guid.NewGuid():N}", issuedToPluginId: null);
        key.MarkInstalled();
        context.SshKeys.Add(key);
        await context.SaveChangesAsync(CancellationToken.None);
    }
}
