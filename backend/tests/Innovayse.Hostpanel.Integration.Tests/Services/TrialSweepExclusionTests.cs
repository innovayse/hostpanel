using Innovayse.Hostpanel.Application.Billing.Commands.SuspendOverdueServicesCron;
using Innovayse.Hostpanel.Application.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.LinkDomainService;
using Innovayse.Hostpanel.Application.Services.Common;
using Innovayse.Hostpanel.Application.Services.Queries.GetRenewableServices;
using Innovayse.Hostpanel.Application.Services.Queries.ListEndOfCycleTerminationsDue;
using Innovayse.Hostpanel.Application.Services.Queries.ListOverdueSuspendedServices;
using Innovayse.Hostpanel.Application.Services.Queries.ListOverdueSuspensionsPastLimit;
using Innovayse.Hostpanel.Application.Services.Queries.ListUndatedRenewingServices;
using Innovayse.Hostpanel.Application.Services.Queries.ListUpcomingServiceRenewals;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// The renewal, reminder, suspension and termination sweeps leave a running trial alone: a service whose
/// trial is <see cref="TrialStatus.Active"/> or <see cref="TrialStatus.AwaitingPayment"/> is never returned,
/// while an ordinary service and a trial already <see cref="TrialStatus.ConvertedToPaid"/> still are.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class TrialSweepExclusionTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The four services every sweep test seeds: ordinary, active trial, awaiting-payment trial, converted trial.</summary>
    /// <param name="Ordinary">A service that never had a trial.</param>
    /// <param name="Active">A trial that is running (<see cref="TrialStatus.Active"/>).</param>
    /// <param name="Awaiting">A converted trial whose invoice is unpaid.</param>
    /// <param name="Converted">A converted trial that was paid.</param>
    private sealed record Seeded(int Ordinary, int Active, int Awaiting, int Converted)
    {
        /// <summary>The services a sweep may return.</summary>
        public int[] Eligible => [Ordinary, Converted];

        /// <summary>The services a sweep must leave out.</summary>
        public int[] Running => [Active, Awaiting];
    }

    /// <summary>The renewal query lists the ordinary and the converted service but not the running trials.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task GetRenewableServices_SkipsRunningTrialsAsync()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var seeded = await SeedAsync(host, TestMoments.Seeded.AddDays(-1));

        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<RenewableServiceDto>>>(
            new GetRenewableServicesQuery(null, TestMoments.Seeded));

        AssertSweep(result.Value!.Select(s => s.Id), seeded);
    }

    /// <summary>The upcoming-renewals list shows the ordinary and the converted service but not the running trials.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ListUpcomingServiceRenewals_SkipsRunningTrialsAsync()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var seeded = await SeedAsync(host, TestMoments.Seeded.AddDays(-1));

        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<Result<PaginatedList<UpcomingServiceRenewalDto>>>(
            new ListUpcomingServiceRenewalsQuery(RenewalWindow.Next30Days, 1, 100));

        AssertSweep(result.Value!.Items.Select(s => s.Id), seeded);
    }

    /// <summary>The undated-renewals sweep lists the ordinary and the converted service but not the running trials.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ListUndatedRenewingServices_SkipsRunningTrialsAsync()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var seeded = await SeedAsync(host, null);

        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<int>>>(new ListUndatedRenewingServicesQuery());

        AssertSweep(result.Value!, seeded);
    }

    /// <summary>The end-of-cycle termination sweep lists the ordinary and the converted service but not the running trials.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ListEndOfCycleTerminationsDue_SkipsRunningTrialsAsync()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var seeded = await SeedAsync(host, TestMoments.Seeded.AddDays(-1));
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var ids = seeded.Eligible.Concat(seeded.Running).ToList();
            await context.ClientServices.Where(s => ids.Contains(s.Id))
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.AutoTerminateEndOfCycle, true), CancellationToken.None);
        }

        using var queryScope = host.Services.CreateScope();
        var bus = queryScope.ServiceProvider.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<EndOfCycleTerminationDto>>>(
            new ListEndOfCycleTerminationsDueQuery(TestMoments.Seeded));

        AssertSweep(result.Value!.Select(d => d.ServiceId), seeded);
    }

    /// <summary>The auto-terminate feed lists the ordinary and the converted overdue-suspended service but not the running trials.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ListOverdueSuspensionsPastLimit_SkipsRunningTrialsAsync()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var seeded = await SeedAsync(host, TestMoments.Seeded.AddDays(10));
        await SuspendAllAsync(host, seeded);

        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<int>>>(
            new ListOverdueSuspensionsPastLimitQuery(TestMoments.Seeded.AddDays(1)));

        AssertSweep(result.Value!, seeded);
    }

    /// <summary>The lift sweep still lists a running trial that is suspended for non-payment, so its suspension can be lifted.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task ListOverdueSuspendedServices_StillListsRunningTrialsAsync()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var seeded = await SeedAsync(host, TestMoments.Seeded.AddDays(10));
        await SuspendAllAsync(host, seeded);

        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<Result<IReadOnlyList<int>>>(new ListOverdueSuspendedServicesQuery());

        result.Value!.Should().Contain(seeded.Eligible.Concat(seeded.Running));
    }

    /// <summary>A running trial service can still be linked to a domain.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task LinkDomainService_SucceedsForARunningTrialAsync()
    {
        await using var host = RenewalTestSeeds.SuppressedHost(factory);
        var seeded = await SeedAsync(host, TestMoments.Seeded.AddDays(10));
        int domainId;
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var clientId = await DomainsTestSeeds.SeedClientAsync(context);
            var domain = RegisteredDomain.Register(
                clientId, $"trial-{Guid.NewGuid():N}"[..17] + ".example", TestMoments.Seeded, TestMoments.Seeded.AddYears(1),
                autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
            context.Domains.Add(domain);
            await context.SaveChangesAsync(CancellationToken.None);
            domainId = domain.Id;
        }

        using var invokeScope = host.Services.CreateScope();
        var bus = invokeScope.ServiceProvider.GetRequiredService<IMessageBus>();
        var result = await bus.InvokeAsync<Result>(new LinkDomainServiceCommand(domainId, seeded.Active));

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>The overdue-suspension cron suspends the ordinary and the converted service but never a running trial.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task SuspendOverdueServicesCron_DoesNotSuspendRunningTrialsAsync()
    {
        using var host = new ClockedHost(factory, configure: RenewalTestSeeds.UseSuppressedGate);
        var seeded = await SeedAsync(host, host.Clock.GetUtcNow().AddDays(10));
        var all = seeded.Eligible.Concat(seeded.Running).ToList();

        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await context.ClientServices.AsNoTracking().Where(s => s.Id == seeded.Ordinary).Select(s => s.ClientId).SingleAsync();
        var now = host.Clock.GetUtcNow();
        var due = now.AddDays(-10);
        var invoice = Invoice.Create(clientId, due, "USD", now);
        foreach (var id in all)
        {
            invoice.AddItem("Hosting", 25m, 1, id);
        }

        invoice.UpdateOptions(due.AddDays(-14), due, null, 0m);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        invoice.MarkOverdue(now);
        await context.SaveChangesAsync(CancellationToken.None);

        await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync(new SuspendOverdueServicesCronCommand(), CancellationToken.None);

        var statuses = await context.ClientServices.AsNoTracking()
            .Where(s => all.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Status);
        statuses[seeded.Ordinary].Should().Be(ServiceStatus.Suspended);
        statuses[seeded.Converted].Should().Be(ServiceStatus.Suspended);
        statuses[seeded.Active].Should().Be(ServiceStatus.Active);
        statuses[seeded.Awaiting].Should().Be(ServiceStatus.Active);
    }

    /// <summary>Suspends every seeded service for non-payment, long ago, straight in the database.</summary>
    /// <param name="host">The host to write through.</param>
    /// <param name="seeded">The seeded services.</param>
    /// <returns>A task.</returns>
    private static async Task SuspendAllAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host, Seeded seeded)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var ids = seeded.Eligible.Concat(seeded.Running).ToList();
        foreach (var service in await context.ClientServices.Where(s => ids.Contains(s.Id)).ToListAsync())
        {
            service.Suspend(TestMoments.Seeded.AddDays(-30), "test", hostingAlreadyUpdated: true, source: SuspensionSource.Overdue);
        }

        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Asserts a sweep returned the eligible services and none of the running trials.</summary>
    /// <param name="returned">The ids the sweep returned.</param>
    /// <param name="seeded">The seeded services.</param>
    private static void AssertSweep(IEnumerable<int> returned, Seeded seeded)
    {
        var ids = returned.ToList();
        ids.Should().Contain(seeded.Eligible);
        ids.Should().NotContain(seeded.Running);
    }

    /// <summary>Seeds the four services (ordinary, active trial, awaiting-payment trial, converted trial), all active and auto-renewing.</summary>
    /// <param name="host">The host to seed through.</param>
    /// <param name="nextRenewalAt">The renewal date every service gets.</param>
    /// <returns>The seeded ids.</returns>
    private static async Task<Seeded> SeedAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host, DateTimeOffset? nextRenewalAt) =>
        await SeedAsync(host.Services, nextRenewalAt);

    /// <summary>Seeds the four services (ordinary, active trial, awaiting-payment trial, converted trial), all active and auto-renewing.</summary>
    /// <param name="host">The clocked host to seed through.</param>
    /// <param name="nextRenewalAt">The renewal date every service gets.</param>
    /// <returns>The seeded ids.</returns>
    private static async Task<Seeded> SeedAsync(ClockedHost host, DateTimeOffset? nextRenewalAt) =>
        await SeedAsync(host.Services, nextRenewalAt);

    /// <summary>Seeds the four services and sets the trial status of three of them straight in the database.</summary>
    /// <param name="services">The services of the host to seed through.</param>
    /// <param name="nextRenewalAt">The renewal date every service gets.</param>
    /// <returns>The seeded ids.</returns>
    private static async Task<Seeded> SeedAsync(IServiceProvider services, DateTimeOffset? nextRenewalAt)
    {
        var ordinary = await RenewalTestSeeds.SeedActiveServiceAsync(services, nextRenewalAt);
        var active = await RenewalTestSeeds.SeedActiveServiceAsync(services, nextRenewalAt);
        var awaiting = await RenewalTestSeeds.SeedActiveServiceAsync(services, nextRenewalAt);
        var converted = await RenewalTestSeeds.SeedActiveServiceAsync(services, nextRenewalAt);

        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        await SetTrialAsync(context, active.ServiceId, TrialStatus.Active);
        await SetTrialAsync(context, awaiting.ServiceId, TrialStatus.AwaitingPayment);
        await SetTrialAsync(context, converted.ServiceId, TrialStatus.ConvertedToPaid);
        return new Seeded(ordinary.ServiceId, active.ServiceId, awaiting.ServiceId, converted.ServiceId);
    }

    /// <summary>Sets one service's trial status straight in the database.</summary>
    /// <param name="context">The data context.</param>
    /// <param name="serviceId">The service.</param>
    /// <param name="status">The trial status to store.</param>
    /// <returns>A task.</returns>
    private static async Task SetTrialAsync(IHostpanelDbContext context, int serviceId, TrialStatus status) =>
        await context.ClientServices.Where(s => s.Id == serviceId)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.TrialStatus, status), CancellationToken.None);
}
