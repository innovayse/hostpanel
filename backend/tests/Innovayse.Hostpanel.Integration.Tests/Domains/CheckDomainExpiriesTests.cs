using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Domains.Commands.CheckDomainExpiries;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the <c>CheckDomainExpiries</c> scheduled job, dispatched through the
/// real bus (<c>IMessageBus.InvokeAsync</c>) exactly as it runs on its schedule.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CheckDomainExpiriesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// An active domain already past its expiry date is marked expired: the query
    /// <c>CheckDomainExpiriesHandler</c> uses to find domains to hand to
    /// <c>MarkDomainExpiredCommand</c>.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckDomainExpiries_MarksAnOverdueDomainExpiredAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.Register(
            clientId, $"{Guid.NewGuid():N}.test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(-1), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        await DomainsTestSeeds.AddActivatedAsync(context, domain, "REF-3", domain.ExpiresAt, DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        await bus.InvokeAsync(new CheckDomainExpiriesCommand(), CancellationToken.None);

        var reloaded = await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domain.Id, CancellationToken.None);
        Assert.Equal(DomainStatus.Expired, reloaded.Status);
    }

    /// <summary>
    /// An active domain expiring within the warning window gets only the nearest warning due: one
    /// expiring in under a day is recorded against the one day warning, not the larger ones.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckDomainExpiries_RecordsAWarningReminderForASoonExpiringDomainAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.Register(
            clientId, $"{Guid.NewGuid():N}.test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(20), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        await DomainsTestSeeds.AddActivatedAsync(context, domain, "REF-4", domain.ExpiresAt, DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        await bus.InvokeAsync(new CheckDomainExpiriesCommand(), CancellationToken.None);

        var reloaded = await context.Domains
            .Include(d => d.Reminders)
            .AsNoTracking()
            .FirstAsync(d => d.Id == domain.Id, CancellationToken.None);
        Assert.Equal(DomainReminderType.OneDayBeforeExpiry, Assert.Single(reloaded.Reminders).ReminderType);
    }

    /// <summary>
    /// A reminder sent for the previous expiry cycle does not silence the warning of the current
    /// one: after a renewal the warnings go out again.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckDomainExpiries_RemindsAgainInTheNextExpiryCycleAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.Register(
            clientId, $"{Guid.NewGuid():N}.test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(10), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        await DomainsTestSeeds.AddActivatedAsync(context, domain, "REF-9", domain.ExpiresAt, DateTimeOffset.UtcNow);
        domain.AddReminder(DomainReminderType.FourteenDaysBeforeExpiry, $"client-{clientId}", DateTimeOffset.UtcNow.AddYears(-1));
        await context.SaveChangesAsync(CancellationToken.None);

        await bus.InvokeAsync(new CheckDomainExpiriesCommand(), CancellationToken.None);

        var reminders = await RemindersOfAsync(context, domain.Id);
        Assert.Equal(2, reminders.Count(r => r.ReminderType == DomainReminderType.FourteenDaysBeforeExpiry));
    }

    /// <summary>
    /// An auto-renewing domain whose renewal line is on an open (Unpaid) invoice gets no reminder
    /// row: the invoice's own reminders already tell the client, and a second "expiring" mail would
    /// contradict them. Fails if the open-invoice skip is removed from the warning loop.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckDomainExpiries_SendsNoWarningWhileARenewalInvoiceIsOpenAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var domain = await SeedAutoRenewDomainAsync(context, "REF-5");
        await SeedRenewalInvoiceAsync(context, domain, cancel: false);

        await bus.InvokeAsync(new CheckDomainExpiriesCommand(), CancellationToken.None);

        Assert.Empty(await RemindersOfAsync(context, domain.Id));
    }

    /// <summary>
    /// An auto-renewing domain with no renewal invoice (the renewal run could not price it, say)
    /// still gets the expiry warning: nothing else tells the client it is about to expire.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckDomainExpiries_WarnsAnAutoRenewDomainWithNoRenewalInvoiceAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var domain = await SeedAutoRenewDomainAsync(context, "REF-6");

        await bus.InvokeAsync(new CheckDomainExpiriesCommand(), CancellationToken.None);

        Assert.NotEmpty(await RemindersOfAsync(context, domain.Id));
    }

    /// <summary>An auto-renewing domain whose renewal invoice was cancelled is warned: a cancelled invoice reminds nobody.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckDomainExpiries_WarnsAnAutoRenewDomainWhoseRenewalInvoiceWasCancelledAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var domain = await SeedAutoRenewDomainAsync(context, "REF-7");
        await SeedRenewalInvoiceAsync(context, domain, cancel: true);

        await bus.InvokeAsync(new CheckDomainExpiriesCommand(), CancellationToken.None);

        Assert.NotEmpty(await RemindersOfAsync(context, domain.Id));
    }

    /// <summary>
    /// With renewal reminders switched off (reminder days cleared) an open renewal invoice reminds
    /// nobody, so the expiry warning is sent.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckDomainExpiries_WarnsDespiteAnOpenInvoiceWhenRemindersAreClearedAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var domain = await SeedAutoRenewDomainAsync(context, "REF-8");
        await SeedRenewalInvoiceAsync(context, domain, cancel: false);

        var row = await context.Settings.FirstAsync(s => s.Key == BillingSettingKeys.ReminderDays, CancellationToken.None);
        var original = row.Value;
        row.UpdateValue(string.Empty);
        await context.SaveChangesAsync(CancellationToken.None);
        try
        {
            await bus.InvokeAsync(new CheckDomainExpiriesCommand(), CancellationToken.None);
        }
        finally
        {
            row.UpdateValue(original);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        Assert.NotEmpty(await RemindersOfAsync(context, domain.Id));
    }

    /// <summary>Seeds an active, auto-renewing domain expiring in five days.</summary>
    /// <param name="context">Data context.</param>
    /// <param name="registrarRef">The registrar reference to activate it with.</param>
    /// <returns>The saved domain.</returns>
    private static async Task<RegisteredDomain> SeedAutoRenewDomainAsync(IHostpanelDbContext context, string registrarRef)
    {
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.Register(
            clientId, $"{Guid.NewGuid():N}.test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(5), autoRenew: true, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        await DomainsTestSeeds.AddActivatedAsync(context, domain, registrarRef, domain.ExpiresAt, DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);
        return domain;
    }

    /// <summary>Seeds an Unpaid renewal invoice for the domain's next year, optionally cancelled.</summary>
    /// <param name="context">Data context.</param>
    /// <param name="domain">The domain.</param>
    /// <param name="cancel">Whether the invoice is cancelled after it is raised.</param>
    /// <returns>A task.</returns>
    private static async Task SeedRenewalInvoiceAsync(IHostpanelDbContext context, RegisteredDomain domain, bool cancel)
    {
        var invoice = Invoice.Create(domain.ClientId, domain.ExpiresAt, "USD", DateTimeOffset.UtcNow);
        invoice.AddDomainRenewal($"Domain renewal: {domain.Name} (1y)", 12m, domain.Id, domain.ExpiresAt, domain.ExpiresAt.AddYears(1));
        if (cancel)
        {
            invoice.Cancel(DateTimeOffset.UtcNow);
        }

        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>The domain's recorded expiry reminders, read fresh.</summary>
    /// <param name="context">Data context.</param>
    /// <param name="domainId">The domain.</param>
    /// <returns>The reminders.</returns>
    private static async Task<IReadOnlyCollection<DomainReminder>> RemindersOfAsync(IHostpanelDbContext context, int domainId)
    {
        var reloaded = await context.Domains
            .Include(d => d.Reminders)
            .AsNoTracking()
            .FirstAsync(d => d.Id == domainId, CancellationToken.None);
        return reloaded.Reminders;
    }

    /// <summary>
    /// One lapsed domain whose save fails does not stop the run: another lapsed domain is still marked
    /// expired and a domain still ahead of its date is still warned, so the warnings phase does not
    /// depend on the marking phase.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckDomainExpiries_OneDomainThatCannotBeMarked_StopsNeitherTheOthersNorTheWarningsAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var poisonedId = 0;
        using var host = FailingSaveHost.Create(
            factory,
            new ManualTimeProvider(now),
            tracker => tracker.Entries<RegisteredDomain>().Any(e => e.State == EntityState.Modified && e.Entity.Id == poisonedId));
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var failing = await SeedActiveDomainAsync(context, now.AddDays(-2));
        var lapsed = await SeedActiveDomainAsync(context, now.AddDays(-1));
        var expiring = await SeedActiveDomainAsync(context, now.AddDays(5));
        poisonedId = failing.Id;

        await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync(new CheckDomainExpiriesCommand(), CancellationToken.None);

        (await StatusOfAsync(context, failing.Id)).Should().Be(DomainStatus.Active);
        (await StatusOfAsync(context, lapsed.Id)).Should().Be(DomainStatus.Expired);
        (await RemindersOfAsync(context, expiring.Id)).Should().NotBeEmpty();
    }

    /// <summary>
    /// One warning whose save fails leaves nothing tracked behind: the next domain's warning is still
    /// recorded, and the failing domain is left unwarned for the next run.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CheckDomainExpiries_OneWarningThatCannotBeSaved_DoesNotStopTheNextAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var poisonedId = 0;
        using var host = FailingSaveHost.Create(
            factory,
            new ManualTimeProvider(now),
            tracker => tracker.Entries<DomainReminder>().Any(e => e.State == EntityState.Added && e.Entity.DomainId == poisonedId));
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var failing = await SeedActiveDomainAsync(context, now.AddDays(4));
        var other = await SeedActiveDomainAsync(context, now.AddDays(5));
        poisonedId = failing.Id;

        await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync(new CheckDomainExpiriesCommand(), CancellationToken.None);

        (await RemindersOfAsync(context, failing.Id)).Should().BeEmpty();
        (await RemindersOfAsync(context, other.Id)).Should().NotBeEmpty();
    }

    /// <summary>Seeds an active domain of a fresh client with the given expiry.</summary>
    /// <param name="context">Data context.</param>
    /// <param name="expiresAt">The domain's expiry.</param>
    /// <returns>The saved domain.</returns>
    private static async Task<RegisteredDomain> SeedActiveDomainAsync(IHostpanelDbContext context, DateTimeOffset expiresAt)
    {
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.Register(
            clientId, $"{Guid.NewGuid():N}.test", expiresAt.AddYears(-1), expiresAt, autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        await DomainsTestSeeds.AddActivatedAsync(context, domain, "REF-CHECK", expiresAt, expiresAt.AddYears(-1));
        await context.SaveChangesAsync(CancellationToken.None);
        return domain;
    }

    /// <summary>The domain's current status, read fresh.</summary>
    /// <param name="context">Data context.</param>
    /// <param name="domainId">The domain.</param>
    /// <returns>The status.</returns>
    private static async Task<DomainStatus> StatusOfAsync(IHostpanelDbContext context, int domainId) =>
        (await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domainId, CancellationToken.None)).Status;
}
