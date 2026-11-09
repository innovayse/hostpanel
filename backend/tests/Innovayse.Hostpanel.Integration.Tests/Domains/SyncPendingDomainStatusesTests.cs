using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.SyncPendingDomainStatuses;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the <c>SyncPendingDomainStatuses</c> scheduled job, dispatched through
/// the real bus (<c>IMessageBus.InvokeAsync</c>) exactly as it runs on its schedule.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SyncPendingDomainStatusesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// A domain in <see cref="DomainStatus.PendingRegistration"/> is activated once the
    /// (stubbed) registrar reports it live: the query this job's pending-status pass uses.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SyncPendingDomainStatuses_ActivatesAPendingRegistrationAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.Register(
            clientId, $"{Guid.NewGuid():N}.test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        context.Domains.Add(domain);
        await context.SaveChangesAsync(CancellationToken.None);
        Assert.Equal(DomainStatus.PendingRegistration, domain.Status);

        await bus.InvokeAsync(new SyncPendingDomainStatusesCommand(), CancellationToken.None);

        var reloaded = await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domain.Id, CancellationToken.None);
        Assert.Equal(DomainStatus.Active, reloaded.Status);
    }

    /// <summary>
    /// Activating a pending registration takes the expiry the registrar reports and moves the due
    /// date with it, instead of keeping the date the order guessed.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SyncPendingDomainStatuses_TakesTheRegistrarExpiryAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.Register(
            clientId, $"{Guid.NewGuid():N}.test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(10), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        context.Domains.Add(domain);
        await context.SaveChangesAsync(CancellationToken.None);

        await bus.InvokeAsync(new SyncPendingDomainStatusesCommand(), CancellationToken.None);

        // The stub registrar reports the domain live until a year from now.
        var reloaded = await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domain.Id, CancellationToken.None);
        Assert.True(reloaded.ExpiresAt > DateTimeOffset.UtcNow.AddDays(300));
        Assert.Equal(reloaded.ExpiresAt, reloaded.NextDueDate);
    }

    /// <summary>
    /// A domain already active is left alone by the pending-status sync: it never matches the
    /// pending-status filter.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SyncPendingDomainStatuses_LeavesAnActiveDomainAloneAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.Register(
            clientId, $"{Guid.NewGuid():N}.test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        await DomainsTestSeeds.AddActivatedAsync(context, domain, "REF-5", domain.ExpiresAt, DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        await bus.InvokeAsync(new SyncPendingDomainStatusesCommand(), CancellationToken.None);

        var reloaded = await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domain.Id, CancellationToken.None);
        Assert.Equal(DomainStatus.Active, reloaded.Status);
        Assert.Equal("REF-5", reloaded.RegistrarRef);
    }

    /// <summary>
    /// One pending domain whose activation cannot be saved is left pending for the next run and does
    /// not poison the next domain's save: the following pending domain is still activated.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SyncPendingDomainStatuses_OneDomainThatCannotBeSaved_DoesNotStopTheNextAsync()
    {
        var poisonedId = 0;
        using var host = FailingSaveHost.Create(
            factory,
            new ManualTimeProvider(DateTimeOffset.UtcNow),
            tracker => tracker.Entries<RegisteredDomain>().Any(e => e.State == EntityState.Modified && e.Entity.Id == poisonedId));
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var failing = await SeedPendingAsync(context);
        var other = await SeedPendingAsync(context);
        poisonedId = failing.Id;

        await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync(new SyncPendingDomainStatusesCommand(), CancellationToken.None);

        var failingAfter = await context.Domains.AsNoTracking().FirstAsync(d => d.Id == failing.Id, CancellationToken.None);
        var otherAfter = await context.Domains.AsNoTracking().FirstAsync(d => d.Id == other.Id, CancellationToken.None);
        Assert.Equal(DomainStatus.PendingRegistration, failingAfter.Status);
        Assert.Equal(DomainStatus.Active, otherAfter.Status);
    }

    /// <summary>Seeds a domain of a fresh client that is still pending registration.</summary>
    /// <param name="context">Data context.</param>
    /// <returns>The saved domain.</returns>
    private static async Task<RegisteredDomain> SeedPendingAsync(IHostpanelDbContext context)
    {
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.Register(
            clientId, $"{Guid.NewGuid():N}.test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        context.Domains.Add(domain);
        await context.SaveChangesAsync(CancellationToken.None);
        return domain;
    }
}
