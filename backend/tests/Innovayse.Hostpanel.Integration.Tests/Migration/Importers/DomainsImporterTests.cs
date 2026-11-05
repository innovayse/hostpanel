using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Migration.Common;
using Innovayse.Hostpanel.Application.Migration.Importers;
using Innovayse.Hostpanel.Application.Migration.Pull;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Domain.Migration.Enums;
using Innovayse.Hostpanel.Domain.Notifications;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Innovayse.Hostpanel.Integration.Tests.Migration.Importers;

/// <summary>
/// <see cref="DomainsImporter"/>, driven end to end through <c>MigrationPullWorker.RunAsync</c>
/// against a <see cref="FakeMigrationPullSource"/>: imports a registered domain for a resolved
/// client, deduplicated by domain name.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DomainsImporterTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A domain for a known client is imported.</summary>
    [Fact]
    public async Task RunAsync_Domains_ImportsADomainForAKnownClientAsync()
    {
        var source = new FakeMigrationPullSource { Totals = MigrationSourceTotals.Empty with { Hosting = new MigrationHostingTotals(Domains: 1) } };
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var email = await MigrationTestSeeds.SeedClientAsync(scope.ServiceProvider, context);
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Domains);
        var domainName = $"migrated-{Guid.NewGuid():N}.test";
        source.SetDatasetJson("domains", $$"""
            [{"ClientEmail":"{{email}}","DomainName":"{{domainName}}","RegisteredAt":"2024-01-01T00:00:00Z","ExpiresAt":"2026-01-01T00:00:00Z","NextDueDate":null,"Registrar":null,"Nameservers":null,"Status":"active","AutoRenew":true,"RecurringAmount":10,"FirstPaymentAmount":10,"PaymentMethod":null,"SubscriptionId":null,"AdminNotes":null}]
            """);

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var domain = await context.Domains.AsNoTracking().FirstOrDefaultAsync(d => d.Name == domainName);
        Assert.NotNull(domain);

        // Idempotent re-import: a domain matched by name is skipped the second time.
        var job2 = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Domains);
        await worker.RunAsync(job2.Id, CancellationToken.None);

        var domains = await context.Domains.AsNoTracking().Where(d => d.Name == domainName).ToListAsync();
        Assert.Single(domains);
    }

    /// <summary>
    /// A domain whose client does not exist here is skipped, and its log row carries the
    /// <see cref="MigrationSkipReason.ClientNotFound"/> reason instead of free text.
    /// </summary>
    [Fact]
    public async Task RunAsync_Domains_SkipsADomainOfAnUnknownClientWithClientNotFoundAsync()
    {
        var source = new FakeMigrationPullSource { Totals = MigrationSourceTotals.Empty with { Hosting = new MigrationHostingTotals(Domains: 1) } };
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Domains);
        var domainName = $"orphan-{Guid.NewGuid():N}.test";
        source.SetDatasetJson("domains", $$"""
            [{"ClientEmail":"nobody-{{Guid.NewGuid():N}}@example.test","DomainName":"{{domainName}}","RegisteredAt":"2024-01-01T00:00:00Z","ExpiresAt":"2026-01-01T00:00:00Z","NextDueDate":null,"Registrar":null,"Nameservers":null,"Status":"active","AutoRenew":true,"RecurringAmount":10,"FirstPaymentAmount":10,"PaymentMethod":null,"SubscriptionId":null,"AdminNotes":null}]
            """);

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var log = await context.MigrationLogs.AsNoTracking()
            .SingleAsync(l => l.JobId == job.Id && l.EntityType == MigrationEntityType.Domains);
        Assert.Equal(MigrationLogAction.Skipped, log.Action);
        Assert.Equal(MigrationSkipReason.ClientNotFound, log.SkipReason);
        Assert.Null(log.Reason);
        Assert.False(await context.Domains.AsNoTracking().AnyAsync(d => d.Name == domainName));
    }

    /// <summary>
    /// A record the domain model rejects (a blank name) fails, and its log row carries only the
    /// <see cref="MigrationSkipReason.ImportFailed"/> reason: the exception text stays in the server
    /// log and never reaches the admin-facing migration log.
    /// </summary>
    [Fact]
    public async Task RunAsync_Domains_LogsAFailingRecordWithImportFailedAndNoExceptionTextAsync()
    {
        var source = new FakeMigrationPullSource { Totals = MigrationSourceTotals.Empty with { Hosting = new MigrationHostingTotals(Domains: 1) } };
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var email = await MigrationTestSeeds.SeedClientAsync(scope.ServiceProvider, context);
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Domains);
        source.SetDatasetJson("domains", $$"""
            [{"ClientEmail":"{{email}}","DomainName":"   ","RegisteredAt":"2024-01-01T00:00:00Z","ExpiresAt":"2026-01-01T00:00:00Z","NextDueDate":null,"Registrar":null,"Nameservers":null,"Status":"active","AutoRenew":true,"RecurringAmount":10,"FirstPaymentAmount":10,"PaymentMethod":null,"SubscriptionId":null,"AdminNotes":null}]
            """);

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var log = await context.MigrationLogs.AsNoTracking()
            .SingleAsync(l => l.JobId == job.Id && l.EntityType == MigrationEntityType.Domains);
        Assert.Equal(MigrationLogAction.Failed, log.Action);
        Assert.Equal(MigrationSkipReason.ImportFailed, log.SkipReason);
        Assert.Null(log.Reason);
    }

    /// <summary>
    /// An expired domain is imported in its source state without raising domain events: no client
    /// mail is sent, no registrar reference is invented, and its price is in the client's billing
    /// currency. Domain events run for real here (only the mail relay is faked), so a raised
    /// registered or expired event would reach the fake sender.
    /// </summary>
    [Fact]
    public async Task RunAsync_Domains_ImportsAnExpiredDomainWithoutEventsOrMailAsync()
    {
        var source = new FakeMigrationPullSource { Totals = MigrationSourceTotals.Empty with { Hosting = new MigrationHostingTotals(Domains: 1) } };
        var emailSender = new FakeEmailSender();
        using var host = MigrationTestSeeds.HostWithFakeSource(factory, source).WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(emailSender);
            }));
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var payerCurrency = scope.ServiceProvider.GetRequiredService<IPayerCurrencyResolver>();
        var email = await MigrationTestSeeds.SeedClientAsync(scope.ServiceProvider, context);
        context.EmailTemplates.Add(EmailTemplate.Create("domain-registered", "en", "Domain registered", "Welcome", null));
        context.EmailTemplates.Add(EmailTemplate.Create("domain-expired", "en", "Domain expired", "Expired", null));
        await context.SaveChangesAsync(CancellationToken.None);
        var job = await MigrationTestSeeds.SeedJobSelectingOnlyAsync(context, MigrationEntityType.Domains);
        var domainName = $"expired-{Guid.NewGuid():N}.test";
        source.SetDatasetJson("domains", $$"""
            [{"ClientEmail":"{{email}}","DomainName":"{{domainName}}","RegisteredAt":"2020-01-01T00:00:00Z","ExpiresAt":"2021-01-01T00:00:00Z","NextDueDate":null,"Registrar":"source","Nameservers":null,"Status":"expired","AutoRenew":false,"RecurringAmount":10,"FirstPaymentAmount":10,"PaymentMethod":null,"SubscriptionId":null,"AdminNotes":null}]
            """);

        var worker = scope.ServiceProvider.GetRequiredService<MigrationPullWorker>();
        await worker.RunAsync(job.Id, CancellationToken.None);

        var domain = await context.Domains.AsNoTracking().SingleAsync(d => d.Name == domainName);
        Assert.Equal(DomainStatus.Expired, domain.Status);
        Assert.Null(domain.RegistrarRef);
        Assert.Equal((await payerCurrency.ForClientAsync(domain.ClientId, CancellationToken.None)).Code, domain.PriceCurrency);
        Assert.Empty(emailSender.Sent);
    }
}
