using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Queries.GetOwnedDomainNames;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// <see cref="GetOwnedDomainNamesQuery"/> through the real <see cref="IMessageBus"/>: which of the
/// named domains a client holds, whatever case the names arrive in.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetOwnedDomainNamesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Only the client's own domains come back, matched without regard to letter case.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_MixedNames_ReturnsOnlyTheClientsOwnInLowerCaseAsync()
    {
        int clientId;
        var mine = $"mine-{Guid.NewGuid():N}"[..18] + ".example";
        var theirs = $"theirs-{Guid.NewGuid():N}"[..18] + ".example";
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            clientId = await DomainsTestSeeds.SeedClientAsync(context);
            var otherClientId = await DomainsTestSeeds.SeedClientAsync(context);
            context.Domains.Add(RegisteredDomain.Register(
                clientId, mine, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base));
            context.Domains.Add(RegisteredDomain.Register(
                otherClientId, theirs, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        Result<IReadOnlyList<string>> result;
        using (var scope = factory.Services.CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            result = await bus.InvokeAsync<Result<IReadOnlyList<string>>>(
                new GetOwnedDomainNamesQuery(clientId, [mine.ToUpperInvariant(), theirs, "missing.example"]));
        }

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEquivalentTo([mine.ToLowerInvariant()]);
    }

    /// <summary>
    /// Asked for live domains only, a domain that is cancelled is not held, while one that is pending or active is; asked for
    /// every domain, the cancelled one counts too (a renewal needs it).
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_LiveOnly_LeavesOutADomainThatIsNoLongerLiveAsync()
    {
        int clientId;
        var pending = $"pending-{Guid.NewGuid():N}"[..18] + ".example";
        var active = $"active-{Guid.NewGuid():N}"[..18] + ".example";
        var cancelled = $"cancelled-{Guid.NewGuid():N}"[..18] + ".example";
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            clientId = await DomainsTestSeeds.SeedClientAsync(context);
            var pendingDomain = RegisteredDomain.Register(
                clientId, pending, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
            var activeDomain = RegisteredDomain.Register(
                clientId, active, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
            activeDomain.Activate("REF-LIVE", DateTimeOffset.UtcNow.AddYears(1), DateTimeOffset.UtcNow);
            var cancelledDomain = RegisteredDomain.Register(
                clientId, cancelled, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
            cancelledDomain.Cancel();
            context.Domains.AddRange(pendingDomain, activeDomain, cancelledDomain);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        string[] names = [pending, active, cancelled];
        using var readScope = factory.Services.CreateScope();
        var bus = readScope.ServiceProvider.GetRequiredService<IMessageBus>();
        var live = await bus.InvokeAsync<Result<IReadOnlyList<string>>>(new GetOwnedDomainNamesQuery(clientId, names, LiveOnly: true));
        var every = await bus.InvokeAsync<Result<IReadOnlyList<string>>>(new GetOwnedDomainNamesQuery(clientId, names));

        live.Value.Should().BeEquivalentTo([pending, active]);
        every.Value.Should().BeEquivalentTo(names);
    }

    /// <summary>
    /// Asked for the domains one order created, the query leaves out a domain the client holds from elsewhere (no order, or
    /// another order), so a held domain is never taken for the line's own registration.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_ForAnOrder_LeavesOutADomainAnotherOrderOrNoOrderCreatedAsync()
    {
        int clientId;
        var own = $"own-{Guid.NewGuid():N}"[..18] + ".example";
        var elsewhere = $"else-{Guid.NewGuid():N}"[..18] + ".example";
        var another = $"other-{Guid.NewGuid():N}"[..18] + ".example";
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            clientId = await DomainsTestSeeds.SeedClientAsync(context);
            context.Domains.AddRange(
                RegisteredDomain.Register(
                    clientId, own, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base, orderId: 4_242),
                RegisteredDomain.Register(
                    clientId, elsewhere, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base),
                RegisteredDomain.Register(
                    clientId, another, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base, orderId: 4_243));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        string[] names = [own, elsewhere, another];
        using var readScope = factory.Services.CreateScope();
        var bus = readScope.ServiceProvider.GetRequiredService<IMessageBus>();
        var created = await bus.InvokeAsync<Result<IReadOnlyList<string>>>(new GetOwnedDomainNamesQuery(clientId, names, LiveOnly: true, OrderId: 4_242));
        var held = await bus.InvokeAsync<Result<IReadOnlyList<string>>>(new GetOwnedDomainNamesQuery(clientId, names, LiveOnly: true));

        created.Value.Should().BeEquivalentTo([own]);
        held.Value.Should().BeEquivalentTo(names);
    }
}
