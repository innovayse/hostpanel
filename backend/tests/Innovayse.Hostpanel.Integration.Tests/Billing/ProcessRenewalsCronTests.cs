using Innovayse.Hostpanel.Application.Billing.Commands.ProcessRenewalsCron;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Domain.Notifications;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Integration.Tests.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="ProcessRenewalsCronHandler"/> against Postgres: the daily run first sweeps paid
/// renewal lines that were never applied (and tells staff about registrar claims nobody resolved),
/// then raises each due client's renewal invoices in its own message, so one client's failure does
/// not stop the rest. Raising never extends a service.
/// <para>
/// Every test runs on a host whose domain-event gate is suppressed, so a payment saved while
/// arranging does not reach the <c>RenewalPaymentReceivedHandler</c> fast path: only the cron can
/// have applied a line.
/// </para>
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ProcessRenewalsCronTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The staff address the alert test reads mail back for.</summary>
    private const string AdminEmail = "cron-ops@example.test";

    /// <summary>Builds a host with a suppressed event gate and a fake SMTP relay.</summary>
    /// <param name="sender">The fake relay the host's mail goes to.</param>
    /// <returns>The isolated host.</returns>
    private WebApplicationFactory<Program> Host(FakeEmailSender sender) =>
        EventHandlerTestHost.WithFakeRelay(factory, sender, adminEmail: AdminEmail);

    /// <summary>Seeds a client, a product and one <see cref="ClientService"/> in the given state.</summary>
    /// <param name="context">The data context to seed through.</param>
    /// <param name="status">The service's status.</param>
    /// <param name="nextRenewalAt">The service's recorded next renewal date, or <see langword="null"/>.</param>
    /// <returns>
    /// The client id, the seeded service, and the renewal date as Postgres actually stored it,
    /// truncated to microseconds, so callers comparing against it after a reload never fail on a
    /// sub-microsecond tick .NET kept and Postgres did not.
    /// </returns>
    private static async Task<SeededRenewalService> SeedServiceAsync(
        IHostpanelDbContext context, ServiceStatus status, DateTimeOffset? nextRenewalAt)
    {
        var client = Client.Create($"subject-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        context.Clients.Add(client);

        var group = ProductGroup.Create($"renewals-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);

        var product = Product.Create(group.Id, "Starter", null, null, null, null, ProductType.SharedHosting, 5m, 50m, DateTimeOffset.UtcNow);
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);

        var service = ClientService.Create(client.Id, product.Id, BillingCycle.Monthly, DateTimeOffset.UtcNow);
        await ServicesTestSeeds.AddActivatedAsync(context, service, "panel-ref", DateTimeOffset.UtcNow);
        service.ChangeBillingTerms(new ServiceBillingTerms(
            BillingCycle: BillingCycle.Monthly, Quantity: 1, FirstPaymentAmount: 5m,
            RecurringAmount: 5m, NextRenewalAt: nextRenewalAt, PaymentMethod: null,
            PromotionCode: null, SubscriptionId: null));

        if (status == ServiceStatus.Suspended)
        {
            service.Suspend(DateTimeOffset.UtcNow);
        }

        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await context.ClientServices
            .AsNoTracking()
            .Where(s => s.Id == service.Id)
            .Select(s => s.NextRenewalAt)
            .FirstAsync(CancellationToken.None);

        return new(client.Id, service, stored);
    }

    /// <summary>
    /// Seeds a paid invoice with one domain renewal line whose registrar call was claimed at
    /// <paramref name="claimedAt"/> and never confirmed.
    /// </summary>
    /// <param name="context">The data context to seed through.</param>
    /// <param name="claimedAt">When the line was claimed.</param>
    /// <returns>The domain and the invoice id.</returns>
    private static async Task<ClaimedDomainLine> SeedClaimedDomainLineAsync(
        IHostpanelDbContext context, DateTimeOffset claimedAt)
    {
        var client = Client.Create($"subject-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);

        // Not auto-renewing, so the cron's raising half leaves this client alone.
        var expiry = DateTimeOffset.UtcNow.AddDays(5);
        var domain = RegisteredDomain.Register(client.Id, $"{Guid.NewGuid():N}.test", DateTimeOffset.UtcNow, expiry, autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        await DomainsTestSeeds.AddActivatedAsync(context, domain, "REF-STUB", domain.ExpiresAt, DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        var invoice = Invoice.Create(client.Id, expiry, "USD", DateTimeOffset.UtcNow);
        invoice.AddDomainRenewal($"{domain.Name} renewal", 12m, domain.Id, expiry, expiry.AddYears(1));
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        invoice.RecordPaidHistory("t", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        // The claim ApplyPaidRenewals sets before the registrar call; written the way it writes it.
        await context.InvoiceItems
            .Where(l => l.InvoiceId == invoice.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(l => l.RenewalAttemptAt, (DateTimeOffset?)claimedAt), CancellationToken.None);
        return new(domain, invoice.Id);
    }

    /// <summary>Polls the fake relay for a staff mail naming <paramref name="domainName"/>.</summary>
    /// <param name="sender">The fake relay.</param>
    /// <param name="domainName">The domain the mail must name.</param>
    /// <param name="attempts">How many 200 ms polls to make before giving up.</param>
    /// <returns>The matching staff mails received.</returns>
    private static Task<IReadOnlyList<EmailMessage>> WaitForAlertAsync(FakeEmailSender sender, string domainName, int attempts = 50) =>
        WaitForAlertCountAsync(sender, domainName, 1, attempts);

    /// <summary>The cron raises renewal invoices for every due client and does not advance the service.</summary>
    [Fact]
    public async Task Cron_RaisesForDueClient_WithoutAdvancing()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (firstClient, firstService, firstDue) = await SeedServiceAsync(context, ServiceStatus.Active, DateTimeOffset.UtcNow.AddDays(10));
        var (secondClient, _, _) = await SeedServiceAsync(context, ServiceStatus.Active, DateTimeOffset.UtcNow.AddDays(3));
        var (notDueClient, _, _) = await SeedServiceAsync(context, ServiceStatus.Active, DateTimeOffset.UtcNow.AddDays(30));
        var (suspendedClient, _, _) = await SeedServiceAsync(context, ServiceStatus.Suspended, DateTimeOffset.UtcNow.AddDays(3));

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new ProcessRenewalsCronCommand(), CancellationToken.None);

        var invoice = await context.Invoices.AsNoTracking().Include(i => i.Items)
            .SingleAsync(i => i.ClientId == firstClient, CancellationToken.None);
        var line = invoice.Items.Should().ContainSingle().Subject;
        line.ClientServiceId.Should().Be(firstService.Id);
        line.RenewalPeriodStart.Should().Be(firstDue);
        (await context.Invoices.AsNoTracking().CountAsync(i => i.ClientId == secondClient)).Should().Be(1);
        (await context.Invoices.AsNoTracking().AnyAsync(i => i.ClientId == notDueClient)).Should().BeFalse();
        (await context.Invoices.AsNoTracking().AnyAsync(i => i.ClientId == suspendedClient)).Should().BeFalse();

        // AsNoTracking(): the seeded service is still tracked in this scope, and a tracked query
        // would return its in-memory values rather than the row the cron's scope wrote.
        var reloaded = await context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == firstService.Id, CancellationToken.None);
        reloaded.NextRenewalAt.Should().Be(firstDue);
    }

    /// <summary>
    /// The candidate horizon is the configured lead time, read per run: with
    /// <c>invoice_days_before</c> raised to 30 a service renewing in 20 days is invoiced, and one in
    /// 40 is not.
    /// </summary>
    [Fact]
    public async Task Cron_CandidateHorizon_FollowsTheConfiguredLeadTime()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (insideClient, _, _) = await SeedServiceAsync(context, ServiceStatus.Active, DateTimeOffset.UtcNow.AddDays(20));
        var (outsideClient, _, _) = await SeedServiceAsync(context, ServiceStatus.Active, DateTimeOffset.UtcNow.AddDays(40));

        var row = await context.Settings.FirstAsync(r => r.Key == BillingSettingKeys.InvoiceDaysBefore, CancellationToken.None);
        var original = row.Value;
        row.UpdateValue("30");
        await context.SaveChangesAsync(CancellationToken.None);
        try
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.InvokeAsync(new ProcessRenewalsCronCommand(), CancellationToken.None);
        }
        finally
        {
            row.UpdateValue(original);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        (await context.Invoices.AsNoTracking().CountAsync(i => i.ClientId == insideClient)).Should().Be(1);
        (await context.Invoices.AsNoTracking().AnyAsync(i => i.ClientId == outsideClient)).Should().BeFalse();
    }

    /// <summary>The sweep applies a paid renewal line whose event was lost.</summary>
    [Fact]
    public async Task Cron_SweepsPaidUnappliedLines()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        // Outside the lead time, so the raising half has nothing to do for this client.
        var (clientId, service, storedDue) = await SeedServiceAsync(context, ServiceStatus.Active, DateTimeOffset.UtcNow.AddDays(40));
        var start = storedDue!.Value;
        var invoice = Invoice.Create(clientId, start, "USD", DateTimeOffset.UtcNow);
        invoice.AddServiceRenewal("Starter renewal", 5m, service.Id, start, start.AddMonths(1));
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);

        // Paid with the event gate suppressed: the PaymentReceivedEvent is "lost", as after a crash.
        invoice.RecordPaidHistory("t", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new ProcessRenewalsCronCommand(), CancellationToken.None);

        var reloaded = await context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == service.Id, CancellationToken.None);
        reloaded.NextRenewalAt.Should().Be(start.AddMonths(1));
        var line = await context.InvoiceItems.AsNoTracking().SingleAsync(l => l.InvoiceId == invoice.Id, CancellationToken.None);
        line.RenewalAppliedAt.Should().NotBeNull();
    }

    /// <summary>One client whose invoice cannot be saved does not stop the others.</summary>
    /// <remarks>
    /// Client A's domain is priced at 10^16 USD by its TLD (the sell prices are jsonb, so nothing
    /// refuses that), which overflows the invoice's <c>numeric(18,4)</c> columns: A's own save throws
    /// a genuine database error inside A's message, not a stub.
    /// </remarks>
    [Fact]
    public async Task Cron_OneClientFailing_DoesNotStopOthers()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var failing = Client.Create($"subject-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        context.Clients.Add(failing);
        var tld = $"f{Guid.NewGuid():N}"[..12];
        var config = TldConfig.Create(tld, RegistrarModule.NameAm, "USD", "USD", true, 0, []);
        config.UpdateSellPrices([], [], new() { [1] = 10_000_000_000_000_000m });
        context.TldConfigs.Add(config);
        await context.SaveChangesAsync(CancellationToken.None);
        var domain = RegisteredDomain.Register(failing.Id, $"example.{tld}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(10), autoRenew: true, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        await DomainsTestSeeds.AddActivatedAsync(context, domain, "REF-STUB", domain.ExpiresAt, DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);
        var (healthyClient, healthyService, _) = await SeedServiceAsync(context, ServiceStatus.Active, DateTimeOffset.UtcNow.AddDays(10));

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var run = () => bus.InvokeAsync(new ProcessRenewalsCronCommand(), CancellationToken.None);

        await run.Should().NotThrowAsync();
        (await context.Invoices.AsNoTracking().AnyAsync(i => i.ClientId == failing.Id)).Should().BeFalse();
        var invoice = await context.Invoices.AsNoTracking().Include(i => i.Items)
            .SingleAsync(i => i.ClientId == healthyClient, CancellationToken.None);
        invoice.Items.Should().ContainSingle(l => l.ClientServiceId == healthyService.Id);
    }

    /// <summary>
    /// A renewal invoice left open with nothing owed (credit covered it, and the process stopped
    /// between the two saves) is marked paid by the next run, with no ledger row, and its renewal
    /// is then applied by the sweep.
    /// </summary>
    [Fact]
    public async Task Cron_PaysZeroTotalOpenRenewalInvoice()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, service, storedDue) = await SeedServiceAsync(context, ServiceStatus.Active, DateTimeOffset.UtcNow.AddDays(40));
        var start = storedDue!.Value;
        var invoice = Invoice.Create(clientId, start, "USD", DateTimeOffset.UtcNow);
        invoice.AddServiceRenewal("Starter renewal", 5m, service.Id, start, start.AddMonths(1));
        invoice.ApplyCredit(5m);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new ProcessRenewalsCronCommand(), CancellationToken.None);

        var reloaded = await context.Invoices.AsNoTracking().FirstAsync(i => i.Id == invoice.Id, CancellationToken.None);
        reloaded.Status.Should().Be(InvoiceStatus.Paid);
        reloaded.GatewayTransactionId.Should().Be(PaymentMethodNames.CreditBalance);
        (await context.Transactions.AsNoTracking().AnyAsync(t => t.InvoiceId == invoice.Id)).Should().BeFalse();
        (await context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == service.Id)).NextRenewalAt.Should().Be(start.AddMonths(1));
    }

    /// <summary>
    /// One zero-total renewal invoice whose save fails does not stop the others: the database refuses
    /// the update of the first invoice, and the second is still marked paid in the same run.
    /// </summary>
    [Fact]
    public async Task Cron_OneFailingZeroTotalInvoiceDoesNotStopTheOthers()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var failing = await SeedZeroTotalRenewalInvoiceAsync(context);
        var healthy = await SeedZeroTotalRenewalInvoiceAsync(context);
        var database = ((DbContext)context).Database;
        var suffix = Guid.NewGuid().ToString("N");
        var function = $"RefuseInvoiceUpdate{suffix}";
        var trigger = $"RefuseInvoiceUpdate{suffix}";

        // A real database refusal for exactly one invoice row, so the cron's save of it fails. DDL takes no
        // parameters, so the statements are composed from the test's own generated names and seeded id.
        var createFunction =
            $"CREATE FUNCTION \"{function}\"() RETURNS trigger AS $$ BEGIN " +
            $"IF NEW.\"Id\" = {failing} THEN RAISE EXCEPTION 'refused for the test'; END IF; RETURN NEW; END; $$ LANGUAGE plpgsql;";
        var createTrigger =
            $"CREATE TRIGGER \"{trigger}\" BEFORE UPDATE ON \"Invoices\" FOR EACH ROW EXECUTE FUNCTION \"{function}\"();";
        var dropTrigger = $"DROP TRIGGER \"{trigger}\" ON \"Invoices\";";
        var dropFunction = $"DROP FUNCTION \"{function}\"();";
        await database.ExecuteSqlRawAsync(createFunction);
        await database.ExecuteSqlRawAsync(createTrigger);
        try
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.InvokeAsync(new ProcessRenewalsCronCommand(), CancellationToken.None);
        }
        finally
        {
            await database.ExecuteSqlRawAsync(dropTrigger);
            await database.ExecuteSqlRawAsync(dropFunction);
        }

        (await context.Invoices.AsNoTracking().FirstAsync(i => i.Id == failing, CancellationToken.None))
            .Status.Should().Be(InvoiceStatus.Unpaid);
        (await context.Invoices.AsNoTracking().FirstAsync(i => i.Id == healthy, CancellationToken.None))
            .Status.Should().Be(InvoiceStatus.Paid);
    }

    /// <summary>Seeds an open renewal invoice for a new service that credit fully covers, so it owes nothing.</summary>
    /// <param name="context">The data context to seed through.</param>
    /// <returns>The invoice id.</returns>
    private static async Task<int> SeedZeroTotalRenewalInvoiceAsync(IHostpanelDbContext context)
    {
        var (clientId, service, storedDue) = await SeedServiceAsync(context, ServiceStatus.Active, DateTimeOffset.UtcNow.AddDays(40));
        var start = storedDue!.Value;
        var invoice = Invoice.Create(clientId, start, "USD", DateTimeOffset.UtcNow);
        invoice.AddServiceRenewal("Starter renewal", 5m, service.Id, start, start.AddMonths(1));
        invoice.ApplyCredit(5m);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }

    /// <summary>
    /// A domain line claimed for the registrar call but never confirmed is reported to staff on
    /// every daily run once the claim is a day old, once per run, until a person resolves the line;
    /// a claim younger than a day is not reported yet, and the sweep never re-applies a claimed line.
    /// </summary>
    [Fact]
    public async Task Cron_AlertsStaleClaimEveryDayUntilResolved()
    {
        var sender = new FakeEmailSender();
        var start = DateTimeOffset.UtcNow;
        var clock = new ManualTimeProvider(start);
        var host = Host(sender).WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<TimeProvider>();
            s.AddSingleton<TimeProvider>(clock);
        }));
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var (stale, staleInvoice) = await SeedClaimedDomainLineAsync(context, start.AddDays(-3));
        var (recent, _) = await SeedClaimedDomainLineAsync(context, start.AddHours(-2));

        // Day one: the three-day-old claim is reported, the two-hour-old one is not yet.
        await bus.InvokeAsync(new ProcessRenewalsCronCommand(), CancellationToken.None);
        var first = await WaitForAlertCountAsync(sender, stale.Name, 1);
        first.Should().ContainSingle();
        first[0].Body.Should().Contain("not confirmed").And.Contain("verify at the registrar");
        (await WaitForAlertAsync(sender, recent.Name, attempts: 5)).Should().BeEmpty();

        // Day two: both claims are a day old or more and unresolved, so each is reported once more.
        clock.Advance(TimeSpan.FromDays(1));
        await bus.InvokeAsync(new ProcessRenewalsCronCommand(), CancellationToken.None);
        (await WaitForAlertCountAsync(sender, stale.Name, 2)).Should().HaveCount(2);
        (await WaitForAlertCountAsync(sender, recent.Name, 1)).Should().ContainSingle();

        // Still claimed and unapplied: the sweep leaves a claimed line to a person.
        var line = await context.InvoiceItems.AsNoTracking().SingleAsync(l => l.InvoiceId == staleInvoice, CancellationToken.None);
        line.RenewalAppliedAt.Should().BeNull();
        line.RenewalAttemptAt.Should().NotBeNull();

        // A person resolves the stale line; day three reports only the one still open.
        await context.InvoiceItems.Where(l => l.InvoiceId == staleInvoice)
            .ExecuteUpdateAsync(set => set.SetProperty(l => l.RenewalAppliedAt, (DateTimeOffset?)clock.GetUtcNow()), CancellationToken.None);
        clock.Advance(TimeSpan.FromDays(1));
        await bus.InvokeAsync(new ProcessRenewalsCronCommand(), CancellationToken.None);
        (await WaitForAlertCountAsync(sender, recent.Name, 2)).Should().HaveCount(2);
        (await WaitForAlertCountAsync(sender, stale.Name, 3, attempts: 5)).Should().HaveCount(2);
    }

    /// <summary>Polls the fake relay until at least <paramref name="count"/> staff mails name <paramref name="domainName"/>.</summary>
    /// <param name="sender">The fake relay.</param>
    /// <param name="domainName">The domain the mails must name.</param>
    /// <param name="count">How many mails to wait for.</param>
    /// <param name="attempts">How many 200 ms polls to make before giving up.</param>
    /// <returns>The matching staff mails received.</returns>
    private static async Task<IReadOnlyList<EmailMessage>> WaitForAlertCountAsync(
        FakeEmailSender sender, string domainName, int count, int attempts = 50)
    {
        bool Matches(EmailMessage m) => m.To == AdminEmail && m.Subject.Contains(domainName, StringComparison.Ordinal);

        for (var i = 0; i < attempts && sender.Sent.Count(Matches) < count; i++)
        {
            await Task.Delay(200);
        }

        return sender.Sent.Where(Matches).ToList();
    }

    /// <summary>
    /// The lift sweep: a service suspended for an overdue legacy invoice that has since been paid
    /// (its payment event lost) is unsuspended; one still on an Overdue invoice, and one an operator
    /// suspended, stay suspended.
    /// </summary>
    [Fact]
    public async Task Cron_LiftsOverdueSuspensionWhoseInvoiceIsNoLongerOverdue()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        // No renewal date, so the raising half leaves these clients alone.
        var (paidClient, paidService, _) = await SeedServiceAsync(context, ServiceStatus.Active, null);
        var (overdueClient, overdueService, _) = await SeedServiceAsync(context, ServiceStatus.Active, null);
        var (_, manualService, _) = await SeedServiceAsync(context, ServiceStatus.Active, null);
        paidService.Suspend(DateTimeOffset.UtcNow, "Overdue invoice", hostingAlreadyUpdated: true, source: SuspensionSource.Overdue);
        overdueService.Suspend(DateTimeOffset.UtcNow, "Overdue invoice", hostingAlreadyUpdated: true, source: SuspensionSource.Overdue);
        manualService.Suspend(DateTimeOffset.UtcNow, "Abuse", hostingAlreadyUpdated: true);
        await context.SaveChangesAsync(CancellationToken.None);

        // Legacy lines: a service link, no renewal period.
        var paid = Invoice.Create(paidClient, DateTimeOffset.UtcNow.AddDays(-10), "USD", DateTimeOffset.UtcNow);
        paid.AddItem("Hosting", 5m, 1, paidService.Id);
        var overdue = Invoice.Create(overdueClient, DateTimeOffset.UtcNow.AddDays(-10), "USD", DateTimeOffset.UtcNow);
        overdue.AddItem("Hosting", 5m, 1, overdueService.Id);
        context.Invoices.AddRange(paid, overdue);
        await context.SaveChangesAsync(CancellationToken.None);
        paid.MarkOverdue(DateTimeOffset.UtcNow);
        overdue.MarkOverdue(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        // Paid with the event gate suppressed: the payment path's unsuspend never ran.
        paid.RecordPaidHistory("t", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.InvokeAsync(new ProcessRenewalsCronCommand(), CancellationToken.None);

        var lifted = await context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == paidService.Id, CancellationToken.None);
        lifted.Status.Should().Be(ServiceStatus.Active);
        lifted.SuspensionSource.Should().BeNull();
        (await context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == overdueService.Id, CancellationToken.None))
            .Status.Should().Be(ServiceStatus.Suspended);
        (await context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == manualService.Id, CancellationToken.None))
            .Status.Should().Be(ServiceStatus.Suspended);
    }

    /// <summary>
    /// Two partial payments saved at the same moment each see only themselves, so neither settles
    /// the invoice although together they cover it. The daily run settles such an invoice.
    /// </summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task OpenInvoiceCoveredByConcurrentPayments_IsSettled()
    {
        var host = Host(new FakeEmailSender());
        int invoiceId;
        using (var seed = host.Services.CreateScope())
        {
            var context = seed.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var client = Client.Create($"subject-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
            context.Clients.Add(client);
            await context.SaveChangesAsync(CancellationToken.None);
            var invoice = Invoice.Create(client.Id, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
            invoice.AddItem("Hosting", 10m, 1);
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync(CancellationToken.None);
            invoiceId = invoice.Id;
        }

        // Both scopes load the invoice before either saves, as two admins recording payments at once.
        using var first = host.Services.CreateScope();
        using var second = host.Services.CreateScope();
        var firstContext = first.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var secondContext = second.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var a = await firstContext.Invoices.Include(i => i.Transactions).FirstAsync(i => i.Id == invoiceId, CancellationToken.None);
        var b = await secondContext.Invoices.Include(i => i.Transactions).FirstAsync(i => i.Id == invoiceId, CancellationToken.None);
        a.AddPayment(DateTimeOffset.UtcNow, "Manual", "half-1", 5m, 0m, null, TransactionSource.Manual);
        b.AddPayment(DateTimeOffset.UtcNow, "Manual", "half-2", 5m, 0m, null, TransactionSource.Manual);
        await firstContext.SaveChangesAsync(CancellationToken.None);
        await secondContext.SaveChangesAsync(CancellationToken.None);

        using var scope = host.Services.CreateScope();
        var readContext = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await readContext.Invoices.AsNoTracking().FirstAsync(i => i.Id == invoiceId, CancellationToken.None)).Status
            .Should().Be(InvoiceStatus.Unpaid, "each save saw only its own half");

        await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync(new ProcessRenewalsCronCommand(), CancellationToken.None);

        var settled = await readContext.Invoices.AsNoTracking().FirstAsync(i => i.Id == invoiceId, CancellationToken.None);
        settled.Status.Should().Be(InvoiceStatus.Paid);
        settled.GatewayTransactionId.Should().BeOneOf("half-1", "half-2");
    }
}
