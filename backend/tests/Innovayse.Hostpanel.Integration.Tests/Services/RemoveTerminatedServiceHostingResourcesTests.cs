using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.RemoveTerminatedServiceHostingResources;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Domain.Services.Events;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// Integration tests for <see cref="RemoveTerminatedServiceHostingResourcesCommand"/>: the websites and databases plugins
/// held on a terminated service are marked removed with no server call and freed for reuse; a running service is refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RemoveTerminatedServiceHostingResourcesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Every live resource becomes removed, the removed one stays as it was, other services are untouched, and a rerun changes nothing.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TerminatedService_MarksItsResourcesRemovedAndLeavesOthersAloneAsync()
    {
        var (_, clientId, serverId, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "hosting-term");
        var other = await SshTestSeeds.SeedServiceAsync(factory, clientId, serverId, "other");
        await SeedAsync(serviceId, serverId, "web");
        await SeedAsync(other, serverId, "other");
        await SshTestSeeds.ChangeServiceQuietlyAsync(factory, serviceId, s => s.Terminate(TestMoments.Seeded, "test", hostingAlreadyUpdated: true));
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var first = await bus.InvokeAsync<Result>(new RemoveTerminatedServiceHostingResourcesCommand(serviceId), CancellationToken.None);
        var again = await bus.InvokeAsync<Result>(new RemoveTerminatedServiceHostingResourcesCommand(serviceId), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        again.IsSuccess.Should().BeTrue();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.ServiceWebsites.AsNoTracking().Where(w => w.ClientServiceId == serviceId).Select(w => w.State).ToListAsync())
            .Should().OnlyContain(state => state == HostingResourceState.Removed);
        (await context.ServiceDatabases.AsNoTracking().Where(d => d.ClientServiceId == serviceId).Select(d => d.State).ToListAsync())
            .Should().OnlyContain(state => state == HostingResourceState.Removed);
        (await context.ServiceWebsites.AsNoTracking().SingleAsync(w => w.ClientServiceId == other)).State.Should().Be(HostingResourceState.Active);
        (await context.ServiceDatabases.AsNoTracking().SingleAsync(d => d.ClientServiceId == other)).State.Should().Be(HostingResourceState.Active);
        SshTestSeeds.Agents(factory).AgentFor(serverId).Calls.Should().BeEmpty("a terminated account takes its sites with it, so no server is asked");
        (await context.ActivityLogs.AsNoTracking().Where(l => l.ClientId == clientId).Select(l => l.Description).ToListAsync())
            .Should().Contain(line => line.StartsWith("Marked website", StringComparison.Ordinal) && line.EndsWith($"terminated service #{serviceId}", StringComparison.Ordinal));
    }

    /// <summary>A service that is still running is refused with <c>SERVICE_NOT_TERMINATED</c> and its resources stay active.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RunningService_IsRefusedAsync()
    {
        var (_, _, serverId, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "hosting-term-running");
        await SeedAsync(serviceId, serverId, "web");
        using var scope = factory.Services.CreateScope();

        var outcome = await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync<Result>(new RemoveTerminatedServiceHostingResourcesCommand(serviceId), CancellationToken.None);

        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_NOT_TERMINATED);
        (await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().ServiceWebsites.AsNoTracking()
                .SingleAsync(w => w.ClientServiceId == serviceId)).State.Should().Be(HostingResourceState.Active);
    }

    /// <summary>An id that names no service is refused with <c>SERVICE_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task UnknownService_IsRefusedAsync()
    {
        using var scope = factory.Services.CreateScope();

        var outcome = await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync<Result>(new RemoveTerminatedServiceHostingResourcesCommand(int.MaxValue), CancellationToken.None);

        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.SERVICE_NOT_FOUND);
    }

    /// <summary>The termination event reaches the handler: the service's resources are marked removed, and an unknown service does not throw.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TerminationEvent_SettlesTheResourcesAndNeverThrowsAsync()
    {
        var (_, clientId, serverId, serviceId) = await SshTestSeeds.OwnerWithServiceAsync(factory, "hosting-term-event");
        await SeedAsync(serviceId, serverId, "web");
        await SshTestSeeds.ChangeServiceQuietlyAsync(factory, serviceId, s => s.Terminate(TestMoments.Seeded, "test", hostingAlreadyUpdated: true));
        using var scope = factory.Services.CreateScope();

        await scope.ServiceProvider.PublishAndWaitAsync(new ClientServiceTerminatedEvent(serviceId, clientId, TestMoments.Seeded));
        var unknown = () => scope.ServiceProvider.PublishAndWaitAsync(
            new ClientServiceTerminatedEvent(ServicesTestSeeds.NoSuchServiceId, 1, TestMoments.Seeded));

        (await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().ServiceWebsites.AsNoTracking()
                .SingleAsync(w => w.ClientServiceId == serviceId)).State.Should().Be(HostingResourceState.Removed);
        await unknown.Should().NotThrowAsync();
    }

    /// <summary>Stores an active website and an active database for a service.</summary>
    /// <param name="serviceId">The service.</param>
    /// <param name="serverId">The server it runs on.</param>
    /// <param name="account">The hosting account.</param>
    /// <returns>A task that completes once they are saved.</returns>
    private async Task SeedAsync(int serviceId, int serverId, string account)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var website = ServiceWebsite.Create(serviceId, serverId, account, "acme.sites", $"{account}-app.example.test", "public_html", null, false);
        website.MarkActive();
        var database = ServiceDatabase.Create(serviceId, serverId, account, "acme.sites", "shop");
        database.MarkProvisioned($"{account}_shop", $"{account}_shop");
        context.ServiceWebsites.Add(website);
        context.ServiceDatabases.Add(database);
        await context.SaveChangesAsync(CancellationToken.None);
    }
}
