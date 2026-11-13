using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.RenewDomain;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Integration.Tests.Billing;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the admin <c>RenewDomain</c> use case:
/// <c>POST /api/domains/{id}/renew</c>, an outright renewal that calls the registrar at once and
/// raises no invoice. The client-facing equivalent, <c>RenewMyDomain</c>, places a paid order
/// instead.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RenewDomainTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Renewing an active domain succeeds and extends its expiry date.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenewDomain_ActiveDomain_Returns204AndExtendsExpiryAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var domainId = await DomainsTestSeeds.SeedActiveDomainAsync(context);
        var originalExpiry = (await context.Domains.AsNoTracking()
            .FirstAsync(d => d.Id == domainId, CancellationToken.None)).ExpiresAt;

        var client = await DomainsTestSeeds.AdminClientAsync(factory);
        var response = await client.PostAsJsonAsync($"/api/domains/{domainId}/renew", new { years = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var reloaded = await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domainId, CancellationToken.None);
        reloaded.ExpiresAt.Should().BeAfter(originalExpiry);
    }

    /// <summary>
    /// A renew body that names an order line is ignored: the renewal succeeds but is not recorded against any line, so a
    /// request cannot make a line believe it already renewed a domain.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenewDomain_BodyNamingAnOrderLine_IsIgnoredAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var domainId = await DomainsTestSeeds.SeedActiveDomainAsync(context);

        var client = await DomainsTestSeeds.AdminClientAsync(factory);
        var response = await client.PostAsJsonAsync($"/api/domains/{domainId}/renew", new { years = 1, orderItemId = 424_299 });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var reloaded = await context.Domains.AsNoTracking().Include(d => d.LineRenewals).FirstAsync(d => d.Id == domainId, CancellationToken.None);
        reloaded.LineRenewals.Should().BeEmpty();
    }

    /// <summary>An unknown domain id answers 404 with <c>DOMAIN_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenewDomain_UnknownDomain_Returns404Async()
    {
        var client = await DomainsTestSeeds.AdminClientAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/domains/{DomainsTestSeeds.UnknownId}/renew", new { years = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(DomainsTestSeeds.DomainNotFoundCode);
    }

    /// <summary>An unauthenticated caller is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenewDomain_WithoutToken_Returns401Async()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/domains/{DomainsTestSeeds.UnknownId}/renew", new { years = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A domain with no registrar reference was never placed at a registrar, so a renewal is
    /// refused with <c>DOMAIN_NO_REGISTRAR_REF</c> and the expiry stays put; extending it locally
    /// would mark a paid domain renewed while it lapses at the registrar.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenewDomain_WithoutRegistrarRef_Returns400AndKeepsExpiryAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.Register(
            clientId, $"{Guid.NewGuid():N}.test", TestMoments.Seeded, DateTimeOffset.UtcNow.AddDays(30),
            autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        context.Domains.Add(domain);
        await context.SaveChangesAsync(CancellationToken.None);
        var originalExpiry = (await context.Domains.AsNoTracking()
            .FirstAsync(d => d.Id == domain.Id, CancellationToken.None)).ExpiresAt;

        var client = await DomainsTestSeeds.AdminClientAsync(factory);
        var response = await client.PostAsJsonAsync($"/api/domains/{domain.Id}/renew", new { years = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await DomainsTestSeeds.ReadErrorCodeAsync(response)).Should().Be(HostpanelErrorCodes.DOMAIN_NO_REGISTRAR_REF);
        var reloaded = await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domain.Id, CancellationToken.None);
        reloaded.ExpiresAt.Should().Be(originalExpiry);
    }

    /// <summary>
    /// When the registrar renews but reports no expiry, the current expiry is extended by the
    /// renewed years, so an early renewal keeps its remaining days.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RenewDomain_RegistrarReportsNoExpiry_ExtendsTheCurrentExpiryAsync()
    {
        var host = factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddScoped<IRegistrarProvider, NoExpiryRegistrar>()));
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.Register(
            clientId, $"{Guid.NewGuid():N}.test", TestMoments.Seeded, DateTimeOffset.UtcNow.AddDays(200),
            autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        await DomainsTestSeeds.AddActivatedAsync(context, domain, "REF-STUB", domain.ExpiresAt, DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);
        var originalExpiry = (await context.Domains.AsNoTracking()
            .FirstAsync(d => d.Id == domain.Id, CancellationToken.None)).ExpiresAt;

        var result = await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync<Result>(new RenewDomainCommand(domain.Id, 2), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var reloaded = await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domain.Id, CancellationToken.None);
        reloaded.ExpiresAt.Should().Be(originalExpiry.AddYears(2));
    }

    /// <summary>
    /// A registrar that names its own reason (here: not configured) is answered with that reason,
    /// not with the generic renewal refusal.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RenewDomain_RegistrarNotConfigured_FailsWithTheRegistrarCodeAsync()
    {
        var result = await RenewWithAsync<NotConfiguredRegistrar>();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.REGISTRAR_NOT_CONFIGURED);
    }

    /// <summary>A plain registrar refusal is answered with the renewal's own upstream code.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RenewDomain_RegistrarRefuses_FailsWithTheRenewalCodeAsync()
    {
        var result = await RenewWithAsync<RefusingRegistrar>();

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.DOMAIN_RENEWAL_UPSTREAM_FAILED);
    }

    /// <summary>
    /// Renewing an expired domain brings it back to active and moves the due date with the new
    /// expiry, in the renewal's own save.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RenewDomain_ExpiredDomain_ReturnsToActiveAndMovesDueDateAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.Register(
            clientId, $"{Guid.NewGuid():N}.test", TestMoments.Seeded, DateTimeOffset.UtcNow.AddDays(-2),
            autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        await DomainsTestSeeds.AddActivatedAsync(context, domain, "REF-STUB", domain.ExpiresAt, DateTimeOffset.UtcNow);
        domain.MarkExpired(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync<Result>(new RenewDomainCommand(domain.Id, 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var reloaded = await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domain.Id, CancellationToken.None);
        reloaded.Status.Should().Be(DomainStatus.Active);
        reloaded.NextDueDate.Should().Be(reloaded.ExpiresAt);
    }

    /// <summary>
    /// A cancelled domain is refused with <c>DOMAIN_RENEW_NOT_ALLOWED</c> before the registrar is
    /// called: the refusing registrar would otherwise answer with its own upstream code.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task RenewDomain_CancelledDomain_RefusesBeforeTheRegistrarAsync()
    {
        var host = factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddScoped<IRegistrarProvider, RefusingRegistrar>()));
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.Register(
            clientId, $"{Guid.NewGuid():N}.test", TestMoments.Seeded, DateTimeOffset.UtcNow.AddDays(-2),
            autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        await DomainsTestSeeds.AddActivatedAsync(context, domain, "REF-STUB", domain.ExpiresAt, DateTimeOffset.UtcNow);
        domain.MarkExpired(DateTimeOffset.UtcNow);
        domain.Cancel();
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync<Result>(new RenewDomainCommand(domain.Id, 1), CancellationToken.None);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.DOMAIN_RENEW_NOT_ALLOWED);
    }

    /// <summary>
    /// Two attempts of one order line renew at once and this one loses: the line still counts as renewed, the client's
    /// activity log names the domain and says the registrar may have renewed it twice, and the expiry on record is the later
    /// of the winner's and the one the registrar reported to the loser.
    /// </summary>
    /// <param name="reportedLater">Whether the registrar reported a later expiry to the losing attempt than the winner stored.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RenewDomain_LosingARaceForTheSameLine_LogsAPossibleDoubleRenewalAndKeepsTheLaterExpiryAsync(bool reportedLater)
    {
        var host = factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddScoped<IRegistrarProvider, RacingRenewalRegistrar>()));
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var domainId = await DomainsTestSeeds.SeedActiveDomainAsync(context);
        var seeded = await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domainId, CancellationToken.None);
        var line = 525_000 + (reportedLater ? 1 : 2);
        var winnerExpiresAt = seeded.ExpiresAt.AddYears(1);
        var reportedExpiresAt = reportedLater ? seeded.ExpiresAt.AddYears(2) : seeded.ExpiresAt.AddMonths(6);
        RacingRenewalRegistrar.Race(factory.Services, seeded.Name, line, winnerExpiresAt, reportedExpiresAt);

        var result = await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync<Result>(new RenewDomainCommand(domainId, 1, line), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        using var readScope = factory.Services.CreateScope();
        var db = readScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var reloaded = await db.Domains.AsNoTracking().Include(d => d.LineRenewals).FirstAsync(d => d.Id == domainId);
        reloaded.LineRenewals.Should().ContainSingle().Which.OrderItemId.Should().Be(line);
        var expected = reportedLater ? reportedExpiresAt : winnerExpiresAt;
        reloaded.ExpiresAt.Should().BeCloseTo(expected, TimeSpan.FromMilliseconds(1));
        reloaded.NextDueDate.Should().BeCloseTo(expected, TimeSpan.FromMilliseconds(1));
        (await db.ActivityLogs.AsNoTracking().CountAsync(
            a => a.ClientId == seeded.ClientId
                && a.Description.Contains($"Domain Possibly Renewed Twice - Domain: {seeded.Name} - Order Line ID: {line}")))
            .Should().Be(1);
    }

    /// <summary>Renews a fresh active domain through a host whose registrar is <typeparamref name="TRegistrar"/>.</summary>
    /// <typeparam name="TRegistrar">The registrar the host resolves.</typeparam>
    /// <returns>The renewal's result.</returns>
    private async Task<Result> RenewWithAsync<TRegistrar>()
        where TRegistrar : class, IRegistrarProvider
    {
        var host = factory.WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddScoped<IRegistrarProvider, TRegistrar>()));
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var domainId = await DomainsTestSeeds.SeedActiveDomainAsync(context);

        return await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync<Result>(new RenewDomainCommand(domainId, 1), CancellationToken.None);
    }
}
