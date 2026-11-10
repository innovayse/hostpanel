using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.SendDomainExpiryWarning;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// <see cref="SendDomainExpiryWarningHandler"/> against Postgres: the one expiry warning a domain is
/// due is recorded once per expiry cycle, and an unknown domain is refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class SendDomainExpiryWarningTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Seeds an active domain expiring in the given number of days.</summary>
    /// <param name="context">Data context.</param>
    /// <param name="expiresInDays">Days from now until the domain expires.</param>
    /// <returns>The saved domain.</returns>
    private static async Task<RegisteredDomain> SeedAsync(IHostpanelDbContext context, int expiresInDays)
    {
        var now = DateTimeOffset.UtcNow;
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.Register(
            clientId, $"{Guid.NewGuid():N}.test", now, now.AddDays(expiresInDays), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        await DomainsTestSeeds.AddActivatedAsync(context, domain, "REF-WARN", domain.ExpiresAt, now);
        await context.SaveChangesAsync(CancellationToken.None);
        return domain;
    }

    /// <summary>A domain inside the warning window gets the nearest warning recorded, and a second command records nothing more.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DomainInsideTheWindow_IsWarnedOncePerCycle()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var domain = await SeedAsync(context, expiresInDays: 5);

        var first = await bus.InvokeAsync<Result>(new SendDomainExpiryWarningCommand(domain.Id), CancellationToken.None);
        var second = await bus.InvokeAsync<Result>(new SendDomainExpiryWarningCommand(domain.Id), CancellationToken.None);

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        var reloaded = await context.Domains.Include(d => d.Reminders).AsNoTracking()
            .FirstAsync(d => d.Id == domain.Id, CancellationToken.None);
        reloaded.Reminders.Should().ContainSingle().Which.ReminderType.Should().Be(DomainReminderType.SevenDaysBeforeExpiry);
    }

    /// <summary>A domain far from its expiry gets no warning and the command still succeeds.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DomainOutsideTheWindow_IsNotWarned()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var domain = await SeedAsync(context, expiresInDays: 200);

        var outcome = await bus.InvokeAsync<Result>(new SendDomainExpiryWarningCommand(domain.Id), CancellationToken.None);

        outcome.IsSuccess.Should().BeTrue();
        var reloaded = await context.Domains.Include(d => d.Reminders).AsNoTracking()
            .FirstAsync(d => d.Id == domain.Id, CancellationToken.None);
        reloaded.Reminders.Should().BeEmpty();
    }

    /// <summary>An id that names no domain is refused with <c>DOMAIN_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownDomain_IsRefused()
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var outcome = await bus.InvokeAsync<Result>(
            new SendDomainExpiryWarningCommand(DomainsTestSeeds.UnknownId), CancellationToken.None);

        outcome.IsFailure.Should().BeTrue();
        outcome.ErrorCode.Should().Be(HostpanelErrorCodes.DOMAIN_NOT_FOUND);
    }
}
