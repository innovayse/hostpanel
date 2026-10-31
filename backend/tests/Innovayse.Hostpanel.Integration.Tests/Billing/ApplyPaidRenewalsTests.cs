using Innovayse.Hostpanel.Application.Billing.Commands.ApplyPaidRenewals;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Notifications;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="ApplyPaidRenewalsCommand"/> against Postgres: a paid-in-full invoice extends its
/// renewed services and renews its renewed domains exactly once, lifts the overdue suspensions the
/// payment cleared, and tells staff about every domain renewal it could not complete.
/// <para>
/// Every test runs on a host whose domain-event gate is suppressed, so paying the invoice while
/// arranging does not fire the <c>RenewalPaymentReceivedHandler</c> fast path: the command each
/// test invokes by hand is the only thing that can have applied a line.
/// </para>
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ApplyPaidRenewalsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The staff address the alert tests read mail back for.</summary>
    private const string AdminEmail = "ops@example.test";

    /// <summary>Builds a host with a suppressed event gate and a fake SMTP relay.</summary>
    /// <param name="sender">The fake relay the host's mail goes to.</param>
    /// <returns>The isolated host.</returns>
    private WebApplicationFactory<Program> Host(FakeEmailSender sender) =>
        EventHandlerTestHost.WithFakeRelay(factory, sender, adminEmail: AdminEmail);

    /// <summary>Seeds a client, a product and one active monthly <see cref="ClientService"/>.</summary>
    /// <param name="context">The data context to seed through.</param>
    /// <param name="nextRenewalAt">The service's recorded next renewal date.</param>
    /// <returns>The seeded service.</returns>
    private static async Task<ClientService> SeedActiveServiceAsync(IHostpanelDbContext context, DateTimeOffset? nextRenewalAt)
    {
        var client = Client.Create($"subject-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        context.Clients.Add(client);

        var group = ProductGroup.Create($"apply-renewals-{Guid.NewGuid():N}", null);
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

        await context.SaveChangesAsync(CancellationToken.None);
        return service;
    }

    /// <summary>Seeds an active, registrar-linked domain expiring at the given moment.</summary>
    /// <param name="context">The data context to seed through.</param>
    /// <param name="expiresAt">The domain's current expiry.</param>
    /// <returns>The seeded domain.</returns>
    /// <remarks>
    /// Not <see cref="DomainsTestSeeds.SeedActiveDomainAsync"/>: that one expires a year out, the
    /// same instant the stub registrar answers a renewal with, so "the expiry moved" would be a
    /// difference of milliseconds. A near expiry makes the move unmistakable.
    /// </remarks>
    private static async Task<RegisteredDomain> SeedDomainAsync(IHostpanelDbContext context, DateTimeOffset expiresAt)
    {
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.Register(
            clientId, $"{Guid.NewGuid():N}.test", DateTimeOffset.UtcNow, expiresAt, autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        await DomainsTestSeeds.AddActivatedAsync(context, domain, "REF-STUB", domain.ExpiresAt, DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);
        return domain;
    }

    /// <summary>Raises an unpaid invoice with one service renewal line for the month after <paramref name="start"/>.</summary>
    /// <param name="context">The data context to seed through.</param>
    /// <param name="service">The renewed service.</param>
    /// <param name="start">Period start.</param>
    /// <returns>The saved invoice (still tracked).</returns>
    private static async Task<Invoice> SeedServiceRenewalInvoiceAsync(IHostpanelDbContext context, ClientService service, DateTimeOffset start)
    {
        var invoice = Invoice.Create(service.ClientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
        invoice.AddServiceRenewal("Starter renewal", 5m, service.Id, start, start.AddMonths(1));
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice;
    }

    /// <summary>Reads a service back from the database, not the scope's identity map.</summary>
    /// <param name="context">The data context.</param>
    /// <param name="id">The service id.</param>
    /// <returns>The stored service.</returns>
    private static Task<ClientService> ReloadServiceAsync(IHostpanelDbContext context, int id) =>
        context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == id, CancellationToken.None);

    /// <summary>Reads an invoice's only line back from the database.</summary>
    /// <param name="context">The data context.</param>
    /// <param name="invoiceId">The invoice id.</param>
    /// <returns>The stored line.</returns>
    private static async Task<InvoiceItem> ReloadLineAsync(IHostpanelDbContext context, int invoiceId)
    {
        var invoice = await context.Invoices.AsNoTracking().Include(i => i.Items)
            .FirstAsync(i => i.Id == invoiceId, CancellationToken.None);
        return invoice.Items.Single();
    }

    /// <summary>Polls the fake relay until a mail to <see cref="AdminEmail"/> arrives (the alert is published, not invoked).</summary>
    /// <param name="sender">The fake relay.</param>
    /// <returns>The staff mails received.</returns>
    private static async Task<IReadOnlyList<EmailMessage>> WaitForAdminMailAsync(FakeEmailSender sender)
    {
        for (var i = 0; i < 50 && !sender.Sent.Any(m => m.To == AdminEmail); i++)
        {
            await Task.Delay(200);
        }

        return sender.Sent.Where(m => m.To == AdminEmail).ToList();
    }

    /// <summary>A fully paid service line extends the service to the period end and is marked applied.</summary>
    [Fact]
    public async Task PaidServiceLine_ExtendsOnce()
    {
        var host = Host(new FakeEmailSender());
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var start = DateTimeOffset.UtcNow.AddDays(5);
        var service = await SeedActiveServiceAsync(context, start);
        var invoice = await SeedServiceRenewalInvoiceAsync(context, service, start);
        invoice.RecordPaidHistory("t", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var line = await ReloadLineAsync(context, invoice.Id);
        line.RenewalAppliedAt.Should().NotBeNull();
        (await ReloadServiceAsync(context, service.Id)).NextRenewalAt.Should().Be(line.RenewalPeriodEnd);

        // Exactly once: the marker, not the call, decides: a second dispatch changes nothing.
        var again = await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);
        again.IsSuccess.Should().BeTrue();
        (await ReloadServiceAsync(context, service.Id)).NextRenewalAt.Should().Be(line.RenewalPeriodEnd);
        (await ReloadLineAsync(context, invoice.Id)).RenewalAppliedAt.Should().Be(line.RenewalAppliedAt);
    }

    /// <summary>
    /// A run that extended the service but crashed before marking the line (the extension is saved by
    /// the Services module in its own scope) is retried without a second period: the line is marked
    /// and the renewal date stays at the paid period's end.
    /// </summary>
    [Fact]
    public async Task ExtensionSavedButMarkerLost_RetryDoesNotExtendTwice()
    {
        var host = Host(new FakeEmailSender());
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var start = DateTimeOffset.UtcNow.AddDays(5);
        var service = await SeedActiveServiceAsync(context, start);
        var invoice = await SeedServiceRenewalInvoiceAsync(context, service, start);
        invoice.RecordPaidHistory("t", DateTimeOffset.UtcNow);
        service.ExtendRenewalTo(start.AddMonths(1));
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var line = await ReloadLineAsync(context, invoice.Id);
        line.RenewalAppliedAt.Should().NotBeNull();
        (await ReloadServiceAsync(context, service.Id)).NextRenewalAt.Should().Be(line.RenewalPeriodEnd);
    }

    /// <summary>
    /// A service line has no claim, so the payment event and the sweep can both extend it. The
    /// runner whose marker save loses to the other runner's marker skips that line and still
    /// applies the invoice's other lines and lifts the overdue suspension.
    /// </summary>
    [Fact]
    public async Task LostServiceMarkerRace_SkipsLineAndContinues()
    {
        var host = Host(new FakeEmailSender());
        using var scope = host.Services.CreateScope();
        using var rivalScope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var rivalContext = rivalScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var start = DateTimeOffset.UtcNow.AddDays(-4);
        var raced = await SeedActiveServiceAsync(context, start);
        var other = await SeedActiveServiceAsync(context, start);
        var invoice = await SeedServiceRenewalInvoiceAsync(context, raced, start);
        invoice.AddServiceRenewal("Starter renewal (second)", 5m, other.Id, start, start.AddMonths(1));
        invoice.MarkOverdue(DateTimeOffset.UtcNow);
        raced.Suspend(DateTimeOffset.UtcNow, "overdue", hostingAlreadyUpdated: true, source: SuspensionSource.Overdue);
        await context.SaveChangesAsync(CancellationToken.None);
        invoice.RecordPaidHistory("t", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);
        var racedLineId = invoice.Items.Single(l => l.ClientServiceId == raced.Id).Id;
        var rivalMarker = DateTimeOffset.UtcNow.AddMinutes(-1);

        // The rival runner's marker lands from its own scope while this scope still tracks the line
        // unapplied: the handler here extends again (a no-op) and its marker save is the one that loses.
        await rivalContext.InvoiceItems.Where(l => l.Id == racedLineId)
            .ExecuteUpdateAsync(set => set.SetProperty(l => l.RenewalAppliedAt, rivalMarker));
        var handler = ActivatorUtilities.CreateInstance<ApplyPaidRenewalsHandler>(scope.ServiceProvider);

        var result = await handler.HandleAsync(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var lines = await rivalContext.InvoiceItems.AsNoTracking()
            .Where(l => l.InvoiceId == invoice.Id).ToListAsync(CancellationToken.None);
        lines.Single(l => l.Id == racedLineId).RenewalAppliedAt.Should().BeCloseTo(rivalMarker, TimeSpan.FromMilliseconds(1));
        var otherLine = lines.Single(l => l.ClientServiceId == other.Id);
        otherLine.RenewalAppliedAt.Should().NotBeNull();
        (await ReloadServiceAsync(rivalContext, other.Id)).NextRenewalAt.Should().Be(otherLine.RenewalPeriodEnd);
        var racedService = await ReloadServiceAsync(rivalContext, raced.Id);
        racedService.Status.Should().Be(ServiceStatus.Active);
        // PostgreSQL keeps microseconds, so the stored date is the expected one to the microsecond.
        racedService.NextRenewalAt.Should().BeCloseTo(start.AddMonths(1), TimeSpan.FromMicroseconds(1));
    }

    /// <summary>A partial manual payment applies nothing.</summary>
    [Fact]
    public async Task PartialPayment_AppliesNothing()
    {
        var host = Host(new FakeEmailSender());
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var start = DateTimeOffset.UtcNow.AddDays(5);
        var service = await SeedActiveServiceAsync(context, start);
        var before = (await ReloadServiceAsync(context, service.Id)).NextRenewalAt;
        var invoice = await SeedServiceRenewalInvoiceAsync(context, service, start);
        invoice.AddPayment(DateTimeOffset.UtcNow, "Manual", $"part-{Guid.NewGuid():N}", 1m, 0m, null, TransactionSource.Manual);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await ReloadServiceAsync(context, service.Id)).NextRenewalAt.Should().Be(before);
        (await ReloadLineAsync(context, invoice.Id)).RenewalAppliedAt.Should().BeNull();
    }

    /// <summary>A partial payment followed by the rest renews once the payments cover the invoice.</summary>
    [Fact]
    public async Task PartialPaymentThenTheRest_Renews()
    {
        var host = Host(new FakeEmailSender());
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var start = DateTimeOffset.UtcNow.AddDays(5);
        var service = await SeedActiveServiceAsync(context, start);
        var invoice = await SeedServiceRenewalInvoiceAsync(context, service, start);
        invoice.AddPayment(DateTimeOffset.UtcNow, "Manual", $"part-{Guid.NewGuid():N}", 1m, 0m, null, TransactionSource.Manual);
        await context.SaveChangesAsync(CancellationToken.None);

        invoice.AddPayment(DateTimeOffset.UtcNow, "Manual", $"rest-{Guid.NewGuid():N}", invoice.AmountDue, 0m, null, TransactionSource.Manual);
        await context.SaveChangesAsync(CancellationToken.None);
        var result = await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var line = await ReloadLineAsync(context, invoice.Id);
        line.RenewalAppliedAt.Should().NotBeNull();
        (await ReloadServiceAsync(context, service.Id)).NextRenewalAt.Should().Be(line.RenewalPeriodEnd);
    }

    /// <summary>An unpaid invoice applies nothing.</summary>
    [Fact]
    public async Task UnpaidInvoice_AppliesNothing()
    {
        var host = Host(new FakeEmailSender());
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var start = DateTimeOffset.UtcNow.AddDays(5);
        var service = await SeedActiveServiceAsync(context, start);
        var before = (await ReloadServiceAsync(context, service.Id)).NextRenewalAt;
        var invoice = await SeedServiceRenewalInvoiceAsync(context, service, start);

        var result = await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await ReloadServiceAsync(context, service.Id)).NextRenewalAt.Should().Be(before);
        (await ReloadLineAsync(context, invoice.Id)).RenewalAppliedAt.Should().BeNull();
    }

    /// <summary>An id that names no invoice is refused with <c>INVOICE_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task UnknownInvoice_IsRefused()
    {
        var host = Host(new FakeEmailSender());
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(BillingTestSeeds.NoSuchId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>A line written before renewals were tracked (no period) is ignored.</summary>
    [Fact]
    public async Task LegacyLine_IsIgnored()
    {
        var host = Host(new FakeEmailSender());
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var service = await SeedActiveServiceAsync(context, DateTimeOffset.UtcNow.AddDays(5));
        var before = (await ReloadServiceAsync(context, service.Id)).NextRenewalAt;
        var invoice = Invoice.Create(service.ClientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
        invoice.AddItem("Starter (legacy line)", 5m, 1, service.Id);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        invoice.RecordPaidHistory("t", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await ReloadServiceAsync(context, service.Id)).NextRenewalAt.Should().Be(before);
        var line = await ReloadLineAsync(context, invoice.Id);
        line.IsRenewal.Should().BeFalse();
        line.RenewalAppliedAt.Should().BeNull();
    }

    /// <summary>An overdue-suspended service is unsuspended once nothing overdue covers it.</summary>
    [Fact]
    public async Task OverdueSuspension_IsLifted()
    {
        var host = Host(new FakeEmailSender());
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var start = DateTimeOffset.UtcNow.AddDays(-4);
        var service = await SeedActiveServiceAsync(context, start);
        var invoice = await SeedServiceRenewalInvoiceAsync(context, service, start);
        invoice.MarkOverdue(DateTimeOffset.UtcNow);
        service.Suspend(DateTimeOffset.UtcNow, "overdue", hostingAlreadyUpdated: true, source: SuspensionSource.Overdue);
        await context.SaveChangesAsync(CancellationToken.None);
        invoice.RecordPaidHistory("t", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);

        var reloaded = await ReloadServiceAsync(context, service.Id);
        reloaded.Status.Should().Be(ServiceStatus.Active);
        reloaded.SuspensionSource.Should().BeNull();
        reloaded.NextRenewalAt.Should().Be((await ReloadLineAsync(context, invoice.Id)).RenewalPeriodEnd);
    }

    /// <summary>A manual suspension survives payment.</summary>
    [Fact]
    public async Task ManualSuspension_Survives()
    {
        var host = Host(new FakeEmailSender());
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var start = DateTimeOffset.UtcNow.AddDays(-4);
        var service = await SeedActiveServiceAsync(context, start);
        var invoice = await SeedServiceRenewalInvoiceAsync(context, service, start);
        service.Suspend(DateTimeOffset.UtcNow, "abuse", hostingAlreadyUpdated: true, source: SuspensionSource.Manual);
        await context.SaveChangesAsync(CancellationToken.None);
        invoice.RecordPaidHistory("t", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);

        var reloaded = await ReloadServiceAsync(context, service.Id);
        reloaded.Status.Should().Be(ServiceStatus.Suspended);
        reloaded.SuspensionSource.Should().Be(SuspensionSource.Manual);

        // The period is still paid for: suspension and renewal are separate questions.
        (await ReloadLineAsync(context, invoice.Id)).RenewalAppliedAt.Should().NotBeNull();
    }

    /// <summary>Another overdue invoice covering the service keeps it suspended.</summary>
    [Fact]
    public async Task OtherOverdueInvoice_KeepsSuspended()
    {
        var host = Host(new FakeEmailSender());
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var start = DateTimeOffset.UtcNow.AddDays(-4);
        var service = await SeedActiveServiceAsync(context, start);
        var invoice = await SeedServiceRenewalInvoiceAsync(context, service, start);
        invoice.MarkOverdue(DateTimeOffset.UtcNow);

        var other = Invoice.Create(service.ClientId, DateTimeOffset.UtcNow.AddDays(-10), "USD", DateTimeOffset.UtcNow);
        other.AddItem("Starter add-on", 3m, 1, service.Id);
        context.Invoices.Add(other);
        await context.SaveChangesAsync(CancellationToken.None);
        other.MarkOverdue(DateTimeOffset.UtcNow);
        service.Suspend(DateTimeOffset.UtcNow, "overdue", hostingAlreadyUpdated: true, source: SuspensionSource.Overdue);
        await context.SaveChangesAsync(CancellationToken.None);
        invoice.RecordPaidHistory("t", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);

        var reloaded = await ReloadServiceAsync(context, service.Id);
        reloaded.Status.Should().Be(ServiceStatus.Suspended);
        reloaded.SuspensionSource.Should().Be(SuspensionSource.Overdue);
        (await ReloadLineAsync(context, invoice.Id)).RenewalAppliedAt.Should().NotBeNull();
    }

    /// <summary>A legacy (no-period) overdue invoice, once paid, still lifts the overdue suspension.</summary>
    [Fact]
    public async Task LegacyOverdueInvoicePaid_LiftsSuspension()
    {
        var host = Host(new FakeEmailSender());
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var service = await SeedActiveServiceAsync(context, DateTimeOffset.UtcNow.AddDays(-4));
        var invoice = Invoice.Create(service.ClientId, DateTimeOffset.UtcNow.AddDays(-4), "USD", DateTimeOffset.UtcNow);
        invoice.AddItem("Starter (legacy line)", 5m, 1, service.Id);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        invoice.MarkOverdue(DateTimeOffset.UtcNow);
        service.Suspend(DateTimeOffset.UtcNow, "overdue", hostingAlreadyUpdated: true, source: SuspensionSource.Overdue);
        await context.SaveChangesAsync(CancellationToken.None);
        invoice.RecordPaidHistory("t", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);

        var reloaded = await ReloadServiceAsync(context, service.Id);
        reloaded.Status.Should().Be(ServiceStatus.Active);
        reloaded.SuspensionSource.Should().BeNull();
    }

    /// <summary>A paid domain line renews at the registrar, syncs the due date and is applied.</summary>
    [Fact]
    public async Task PaidDomainLine_Renews()
    {
        var sender = new FakeEmailSender();
        var host = Host(sender);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var domain = await SeedDomainAsync(context, DateTimeOffset.UtcNow.AddDays(10));
        var expiry = await context.Domains.AsNoTracking().Where(d => d.Id == domain.Id).Select(d => d.ExpiresAt).FirstAsync();
        var invoice = Invoice.Create(domain.ClientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
        invoice.AddDomainRenewal($"{domain.Name} renewal", 12m, domain.Id, expiry, expiry.AddYears(1));
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        invoice.RecordPaidHistory("t", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var reloaded = await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domain.Id);

        // The stub registrar answers every renewal with now + 1 year.
        reloaded.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow.AddDays(300));
        reloaded.NextDueDate.Should().Be(reloaded.ExpiresAt);
        (await ReloadLineAsync(context, invoice.Id)).RenewalAppliedAt.Should().NotBeNull();

        // Applied once: a second dispatch neither renews again nor alerts.
        await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);
        (await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domain.Id)).ExpiresAt.Should().Be(reloaded.ExpiresAt);
        await Task.Delay(1000);
        sender.Sent.Should().NotContain(m => m.To == AdminEmail);
    }

    /// <summary>A domain already renewed another way is marked applied and staff are told, not silently skipped.</summary>
    [Fact]
    public async Task AlreadyRenewedDomain_MarksAppliedAndAlerts()
    {
        var sender = new FakeEmailSender();
        var host = Host(sender);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var domain = await SeedDomainAsync(context, DateTimeOffset.UtcNow.AddYears(1));
        var expiry = await context.Domains.AsNoTracking().Where(d => d.Id == domain.Id).Select(d => d.ExpiresAt).FirstAsync();

        // The invoice was raised for the period starting a year ago; the domain moved on since.
        var invoice = Invoice.Create(domain.ClientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
        invoice.AddDomainRenewal($"{domain.Name} renewal", 12m, domain.Id, expiry.AddYears(-1), expiry);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        invoice.RecordPaidHistory("t", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await ReloadLineAsync(context, invoice.Id)).RenewalAppliedAt.Should().NotBeNull();
        (await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domain.Id)).ExpiresAt.Should().Be(expiry);
        var mails = await WaitForAdminMailAsync(sender);
        mails.Should().ContainSingle();
        mails[0].Subject.Should().Contain("Domain renewal needs attention").And.Contain(domain.Name);
    }

    /// <summary>
    /// An expiry nudged by hours after the invoice was raised (an admin edit, the registrar sync
    /// normalising the time of day) is not "already renewed": the paid line still renews the domain.
    /// </summary>
    [Fact]
    public async Task ExpiryNudgedByHours_StillRenews()
    {
        var sender = new FakeEmailSender();
        var host = Host(sender);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var domain = await SeedDomainAsync(context, DateTimeOffset.UtcNow.AddDays(10));
        var expiry = await context.Domains.AsNoTracking().Where(d => d.Id == domain.Id).Select(d => d.ExpiresAt).FirstAsync();
        var invoice = Invoice.Create(domain.ClientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
        invoice.AddDomainRenewal($"{domain.Name} renewal", 12m, domain.Id, expiry, expiry.AddYears(1));
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        invoice.RecordPaidHistory("t", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        var nudged = expiry.AddHours(9);
        await context.Domains.Where(d => d.Id == domain.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(d => d.ExpiresAt, nudged), CancellationToken.None);

        var result = await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        // The stub registrar answers every renewal with now + 1 year.
        (await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domain.Id)).ExpiresAt
            .Should().BeAfter(DateTimeOffset.UtcNow.AddDays(300));
        (await ReloadLineAsync(context, invoice.Id)).RenewalAppliedAt.Should().NotBeNull();
        await Task.Delay(1000);
        sender.Sent.Should().NotContain(m => m.To == AdminEmail, "a renewed domain needs no staff attention");
    }

    /// <summary>A registrar refusal leaves the line unapplied (the sweep retries) and alerts staff.</summary>
    [Fact]
    public async Task RegistrarRefusal_LeavesUnapplied()
    {
        var sender = new FakeEmailSender();
        var host = Host(sender).WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddScoped<IRegistrarProvider, RefusingRegistrar>()));
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var domain = await SeedDomainAsync(context, DateTimeOffset.UtcNow.AddDays(10));
        var expiry = await context.Domains.AsNoTracking().Where(d => d.Id == domain.Id).Select(d => d.ExpiresAt).FirstAsync();
        var invoice = Invoice.Create(domain.ClientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
        invoice.AddDomainRenewal($"{domain.Name} renewal", 12m, domain.Id, expiry, expiry.AddYears(1));
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        invoice.RecordPaidHistory("t", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var line = await ReloadLineAsync(context, invoice.Id);
        line.RenewalAppliedAt.Should().BeNull();
        line.RenewalAttemptAt.Should().BeNull("a clean refusal renewed nothing, so the claim is released for the sweep");
        (await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domain.Id)).ExpiresAt.Should().Be(expiry);
        var mails = await WaitForAdminMailAsync(sender);
        mails.Should().ContainSingle();
        mails[0].Subject.Should().Contain(domain.Name);
    }

    /// <summary>
    /// A paid domain with no registrar reference is not extended locally and not retried for ever:
    /// the registrar is never called, the line is closed, the expiry stays put and staff are alerted
    /// to review a refund.
    /// </summary>
    [Fact]
    public async Task DomainWithoutRegistrarRef_IsClosedAndAlerted()
    {
        var sender = new FakeEmailSender();
        var host = Host(sender).WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddScoped<IRegistrarProvider, CountingRegistrar>()));
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var clientId = await DomainsTestSeeds.SeedClientAsync(context);
        var domain = RegisteredDomain.Register(
            clientId, $"{Guid.NewGuid():N}.test", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(10), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base);
        context.Domains.Add(domain);
        await context.SaveChangesAsync(CancellationToken.None);
        var expiry = await context.Domains.AsNoTracking().Where(d => d.Id == domain.Id).Select(d => d.ExpiresAt).FirstAsync();
        var invoice = Invoice.Create(domain.ClientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
        invoice.AddDomainRenewal($"{domain.Name} renewal", 12m, domain.Id, expiry, expiry.AddYears(1));
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        invoice.RecordPaidHistory("t", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        TestRegistrar.RenewCallsFor(domain.Name).Should().Be(0);
        (await ReloadLineAsync(context, invoice.Id)).RenewalAppliedAt.Should().NotBeNull("the line is closed, not left for a daily retry");
        (await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domain.Id)).ExpiresAt.Should().Be(expiry);
        var mails = await WaitForAdminMailAsync(sender);
        mails.Should().ContainSingle();
        mails[0].Subject.Should().Contain(domain.Name);
        mails[0].Body.Should().Contain("not linked to a registrar");
    }

    /// <summary>After a clean refusal the claim is free again, so the next run with a working registrar renews.</summary>
    [Fact]
    public async Task RefusalReleasesClaim_SoTheNextRunRenews()
    {
        var refusing = Host(new FakeEmailSender()).WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddScoped<IRegistrarProvider, RefusingRegistrar>()));
        var invoiceId = 0;
        var domainId = 0;
        using (var scope = refusing.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var (domain, invoice) = await SeedPaidDomainRenewalAsync(context, DateTimeOffset.UtcNow.AddDays(10));
            invoiceId = invoice.Id;
            domainId = domain.Id;
            await scope.ServiceProvider.GetRequiredService<IMessageBus>()
                .InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoiceId), CancellationToken.None);
            (await ReloadLineAsync(context, invoiceId)).RenewalAppliedAt.Should().BeNull();
        }

        using (var scope = Host(new FakeEmailSender()).Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>()
                .InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoiceId), CancellationToken.None);

            var line = await ReloadLineAsync(context, invoiceId);
            line.RenewalAppliedAt.Should().NotBeNull();
            line.RenewalAttemptAt.Should().NotBeNull("the winning run's claim is kept as the record of the registrar call");
            (await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domainId)).ExpiresAt
                .Should().BeAfter(DateTimeOffset.UtcNow.AddDays(300));
        }
    }

    /// <summary>A line another runner already claimed is left alone: the registrar is not called and the expiry does not move.</summary>
    [Fact]
    public async Task ClaimedLine_IsNotRenewedAgain()
    {
        var host = Host(new FakeEmailSender()).WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddScoped<IRegistrarProvider, CountingRegistrar>()));
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var (domain, invoice) = await SeedPaidDomainRenewalAsync(context, DateTimeOffset.UtcNow.AddDays(10));
        var expiry = await context.Domains.AsNoTracking().Where(d => d.Id == domain.Id).Select(d => d.ExpiresAt).FirstAsync();
        var lineId = invoice.Items.Single().Id;
        await context.InvoiceItems.Where(l => l.Id == lineId)
            .ExecuteUpdateAsync(set => set.SetProperty(l => l.RenewalAttemptAt, DateTimeOffset.UtcNow.AddMinutes(-1)));

        var result = await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        TestRegistrar.RenewCallsFor(domain.Name).Should().Be(0);
        (await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domain.Id)).ExpiresAt.Should().Be(expiry);
        var line = await ReloadLineAsync(context, invoice.Id);
        line.RenewalAppliedAt.Should().BeNull();
        line.RenewalAttemptAt.Should().NotBeNull();
    }

    /// <summary>
    /// A runner that read the line unclaimed but loses the claim save to another runner backs off:
    /// the registrar is not called, the other runner's claim stays, and the lost claim is not
    /// retried by a later save in the same scope.
    /// </summary>
    [Fact]
    public async Task LostClaimRace_BacksOffWithoutRenewing()
    {
        var host = Host(new FakeEmailSender()).WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddScoped<IRegistrarProvider, CountingRegistrar>()));
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (domain, invoice) = await SeedPaidDomainRenewalAsync(context, DateTimeOffset.UtcNow.AddDays(10));
        var expiry = await context.Domains.AsNoTracking().Where(d => d.Id == domain.Id).Select(d => d.ExpiresAt).FirstAsync();
        var lineId = invoice.Items.Single().Id;
        var rivalClaim = DateTimeOffset.UtcNow.AddMinutes(-1);

        // The scope still tracks the line as unclaimed, so the handler reads it unclaimed while the
        // database already holds the rival runner's claim: its own claim save is the one that loses.
        await context.InvoiceItems.Where(l => l.Id == lineId)
            .ExecuteUpdateAsync(set => set.SetProperty(l => l.RenewalAttemptAt, rivalClaim));
        var handler = ActivatorUtilities.CreateInstance<ApplyPaidRenewalsHandler>(scope.ServiceProvider);

        var result = await handler.HandleAsync(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        TestRegistrar.RenewCallsFor(domain.Name).Should().Be(0);
        (await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domain.Id)).ExpiresAt.Should().Be(expiry);
        invoice.Items.Single().RenewalAttemptAt.Should().NotBeNull("the lost claim reloads the rival's claim into the tracked line");
        await context.SaveChangesAsync(CancellationToken.None);
        var line = await ReloadLineAsync(context, invoice.Id);
        line.RenewalAppliedAt.Should().BeNull();
        line.RenewalAttemptAt.Should().BeCloseTo(rivalClaim, TimeSpan.FromMilliseconds(1));
    }

    /// <summary>
    /// A registrar call that throws has an unknown outcome: the claim stays, the line stays
    /// unapplied, staff are told, and a second run does not call the registrar again.
    /// </summary>
    [Fact]
    public async Task RegistrarThrow_KeepsClaimAndIsNotRetried()
    {
        var sender = new FakeEmailSender();
        var host = Host(sender).WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddScoped<IRegistrarProvider, ThrowingRegistrar>()));
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var (domain, invoice) = await SeedPaidDomainRenewalAsync(context, DateTimeOffset.UtcNow.AddDays(10));

        var result = await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue("one line's unknown outcome must not fail the invoice's other lines");
        TestRegistrar.RenewCallsFor(domain.Name).Should().Be(1);
        var line = await ReloadLineAsync(context, invoice.Id);
        line.RenewalAppliedAt.Should().BeNull();
        line.RenewalAttemptAt.Should().NotBeNull();
        var mails = await WaitForAdminMailAsync(sender);
        mails.Should().ContainSingle();
        mails[0].Body.Should().Contain("Registrar outcome unknown");

        await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);

        TestRegistrar.RenewCallsFor(domain.Name).Should().Be(1);
        (await ReloadLineAsync(context, invoice.Id)).RenewalAppliedAt.Should().BeNull();
    }

    /// <summary>A transferred-away domain cannot be renewed: the line is closed, staff are told, and the registrar is not called.</summary>
    [Fact]
    public async Task TransferredDomain_IsClosedAndAlerted()
    {
        var sender = new FakeEmailSender();
        var host = Host(sender).WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddScoped<IRegistrarProvider, CountingRegistrar>()));
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var (domain, invoice) = await SeedPaidDomainRenewalAsync(context, DateTimeOffset.UtcNow.AddDays(10));
        domain.MarkTransferred();
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        TestRegistrar.RenewCallsFor(domain.Name).Should().Be(0);
        (await ReloadLineAsync(context, invoice.Id)).RenewalAppliedAt.Should().NotBeNull();
        var mails = await WaitForAdminMailAsync(sender);
        mails.Should().ContainSingle();
        mails[0].Body.Should().Contain("cannot be renewed");
    }

    /// <summary>A domain deleted after it was invoiced closes its line and tells staff.</summary>
    [Fact]
    public async Task DeletedDomain_IsClosedAndAlerted()
    {
        var sender = new FakeEmailSender();
        var host = Host(sender);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var (domain, invoice) = await SeedPaidDomainRenewalAsync(context, DateTimeOffset.UtcNow.AddDays(10));
        await context.Domains.Where(d => d.Id == domain.Id).ExecuteDeleteAsync();

        var result = await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await ReloadLineAsync(context, invoice.Id)).RenewalAppliedAt.Should().NotBeNull();
        var mails = await WaitForAdminMailAsync(sender);
        mails.Should().ContainSingle();
        mails[0].Body.Should().Contain("no longer exists");
    }

    /// <summary>A freshly raised Unpaid next-period invoice for the service does not block lifting the overdue suspension.</summary>
    [Fact]
    public async Task FreshUnpaidInvoice_DoesNotBlockUnsuspend()
    {
        var host = Host(new FakeEmailSender());
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var start = DateTimeOffset.UtcNow.AddDays(-4);
        var service = await SeedActiveServiceAsync(context, start);
        var invoice = await SeedServiceRenewalInvoiceAsync(context, service, start);
        invoice.MarkOverdue(DateTimeOffset.UtcNow);

        var next = Invoice.Create(service.ClientId, DateTimeOffset.UtcNow.AddDays(20), "USD", DateTimeOffset.UtcNow);
        next.AddServiceRenewal("Starter renewal (next)", 5m, service.Id, start.AddMonths(1), start.AddMonths(2));
        context.Invoices.Add(next);
        service.Suspend(DateTimeOffset.UtcNow, "overdue", hostingAlreadyUpdated: true, source: SuspensionSource.Overdue);
        await context.SaveChangesAsync(CancellationToken.None);
        invoice.RecordPaidHistory("t", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);

        (await ReloadServiceAsync(context, service.Id)).Status.Should().Be(ServiceStatus.Active);
    }

    /// <summary>Seeds a domain expiring at <paramref name="expiresAt"/> and a paid invoice renewing it for one year from then.</summary>
    /// <param name="context">The data context to seed through.</param>
    /// <param name="expiresAt">The domain's current expiry.</param>
    /// <returns>The domain and the paid invoice (both still tracked).</returns>
    private static async Task<PaidDomainRenewal> SeedPaidDomainRenewalAsync(
        IHostpanelDbContext context, DateTimeOffset expiresAt)
    {
        var domain = await SeedDomainAsync(context, expiresAt);
        var expiry = await context.Domains.AsNoTracking().Where(d => d.Id == domain.Id).Select(d => d.ExpiresAt).FirstAsync();
        var invoice = Invoice.Create(domain.ClientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
        invoice.AddDomainRenewal($"{domain.Name} renewal", 12m, domain.Id, expiry, expiry.AddYears(1));
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        invoice.RecordPaidHistory("t", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);
        return new(domain, invoice);
    }
}
