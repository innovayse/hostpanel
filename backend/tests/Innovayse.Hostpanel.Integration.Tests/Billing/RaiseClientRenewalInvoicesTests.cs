using Innovayse.Hostpanel.Application.Billing.Commands.ApplyPaidRenewals;
using Innovayse.Hostpanel.Application.Billing.Commands.RaiseClientRenewalInvoices;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Innovayse.Hostpanel.Domain.Notifications;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="RaiseClientRenewalInvoicesCommand"/> against Postgres: one client's services and
/// domains inside the lead time (the default 14 days) are invoiced exactly once for their next
/// period, priced and credited, and nothing is extended by raising the invoice.
/// <para>
/// Every test runs on a host whose domain-event gate is suppressed, so a credit-paid invoice does
/// not reach the <c>RenewalPaymentReceivedHandler</c> fast path: what a test observes is what the
/// command itself did.
/// </para>
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RaiseClientRenewalInvoicesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The staff address the alert tests read mail back for.</summary>
    private const string AdminEmail = "renewals-ops@example.test";

    /// <summary>Builds a host with a suppressed event gate and a fake SMTP relay.</summary>
    /// <param name="sender">The fake relay the host's mail goes to.</param>
    /// <returns>The isolated host.</returns>
    private WebApplicationFactory<Program> Host(FakeEmailSender sender) =>
        EventHandlerTestHost.WithFakeRelay(factory, sender, adminEmail: AdminEmail);

    /// <summary>Seeds a client with no credit and default settings.</summary>
    /// <param name="context">The data context to seed through.</param>
    /// <returns>The saved client (still tracked).</returns>
    private static async Task<Client> SeedClientAsync(IHostpanelDbContext context)
    {
        var client = Client.Create($"subject-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);
        return client;
    }

    /// <summary>Seeds a product and one active <see cref="ClientService"/> renewing at the given moment.</summary>
    /// <param name="context">The data context to seed through.</param>
    /// <param name="nextRenewalAt">The service's recorded next renewal date.</param>
    /// <param name="cycle">The service's billing cycle.</param>
    /// <param name="autoRenew">Whether the service renews automatically.</param>
    /// <param name="clientId">The owning client, or <see langword="null"/> to seed a new one.</param>
    /// <param name="recurringAmount">The service's recurring price.</param>
    /// <param name="type">The product's type.</param>
    /// <param name="sku">The product's SKU, which is unique across products, or <see langword="null"/> for none.</param>
    /// <returns>
    /// The client id, the seeded service, and the renewal date as Postgres actually stored it,
    /// truncated to microseconds, so comparisons after a reload never fail on a dropped tick.
    /// </returns>
    private static async Task<SeededRenewalService> SeedServiceAsync(
        IHostpanelDbContext context, DateTimeOffset nextRenewalAt, BillingCycle cycle = BillingCycle.Monthly,
        bool autoRenew = true, int? clientId = null, decimal recurringAmount = 5m, ProductType type = ProductType.SharedHosting,
        string? sku = null)
    {
        var ownerId = clientId ?? (await SeedClientAsync(context)).Id;

        var group = ProductGroup.Create($"raise-renewals-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);

        var product = Product.Create(group.Id, "Starter", null, null, null, null, type, 5m, 50m, DateTimeOffset.UtcNow);
        product.SetSku(sku);
        product.SetMetadata("{\"tier\":\"basic\"}");
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);

        var service = ClientService.Create(ownerId, product.Id, cycle, DateTimeOffset.UtcNow);
        await ServicesTestSeeds.AddActivatedAsync(context, service, "panel-ref", DateTimeOffset.UtcNow);
        service.ChangeBillingTerms(new ServiceBillingTerms(
            BillingCycle: cycle, Quantity: 1, FirstPaymentAmount: 5m,
            RecurringAmount: recurringAmount, NextRenewalAt: nextRenewalAt, PaymentMethod: null,
            PromotionCode: null, SubscriptionId: null));
        service.ConfigureAutomation(autoRenew, false, null, false, null);

        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await context.ClientServices.AsNoTracking()
            .Where(s => s.Id == service.Id)
            .Select(s => s.NextRenewalAt)
            .FirstAsync(CancellationToken.None);

        return new(ownerId, service, stored);
    }

    /// <summary>
    /// Seeds an active, auto-renewing domain on a TLD of its own, and that TLD's configuration
    /// with the given renewal prices.
    /// </summary>
    /// <param name="context">The data context to seed through.</param>
    /// <param name="clientId">The owning client.</param>
    /// <param name="expiresAt">The domain's expiry.</param>
    /// <param name="sellRenew">The TLD's renewal sell prices keyed by years.</param>
    /// <param name="sellCurrency">The currency the TLD's sell prices are in.</param>
    /// <param name="registrationPeriod">The domain's registration period in years.</param>
    /// <returns>The seeded domain and its expiry as Postgres stored it.</returns>
    private static async Task<SeededRenewableDomain> SeedDomainAsync(
        IHostpanelDbContext context, int clientId, DateTimeOffset expiresAt, Dictionary<int, decimal> sellRenew,
        string sellCurrency = "USD", int registrationPeriod = 1)
    {
        var tld = $"r{Guid.NewGuid():N}"[..12];
        var config = TldConfig.Create(tld, RegistrarModule.NameAm, "USD", sellCurrency, true, 0, []);
        config.UpdateSellPrices([], [], sellRenew);
        context.TldConfigs.Add(config);

        var domain = RegisteredDomain.Register(
            clientId, $"example.{tld}", DateTimeOffset.UtcNow, expiresAt, autoRenew: true, whoisPrivacy: false, registrationPeriod: registrationPeriod, priceCurrency: TestCurrencies.Base);
        await DomainsTestSeeds.AddActivatedAsync(context, domain, "REF-STUB", domain.ExpiresAt, DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        var stored = await context.Domains.AsNoTracking()
            .Where(d => d.Id == domain.Id)
            .Select(d => d.ExpiresAt)
            .FirstAsync(CancellationToken.None);
        return new(domain, stored);
    }

    /// <summary>Raises the client's renewal invoices.</summary>
    /// <param name="bus">The message bus.</param>
    /// <param name="clientId">The client.</param>
    /// <returns>The command's result.</returns>
    private static Task<Result<int>> RaiseAsync(IMessageBus bus, int clientId) =>
        bus.InvokeAsync<Result<int>>(new RaiseClientRenewalInvoicesCommand(clientId), CancellationToken.None);

    /// <summary>Reads the client's invoices back from the database, with their lines.</summary>
    /// <param name="context">The data context.</param>
    /// <param name="clientId">The client.</param>
    /// <returns>The stored invoices, oldest first.</returns>
    private static Task<List<Invoice>> InvoicesOfAsync(IHostpanelDbContext context, int clientId) =>
        context.Invoices.AsNoTracking().Include(i => i.Items)
            .Where(i => i.ClientId == clientId)
            .OrderBy(i => i.Id)
            .ToListAsync(CancellationToken.None);

    /// <summary>Reads a service back from the database, not the scope's identity map.</summary>
    /// <param name="context">The data context.</param>
    /// <param name="id">The service id.</param>
    /// <returns>The stored service.</returns>
    private static Task<ClientService> ReloadServiceAsync(IHostpanelDbContext context, int id) =>
        context.ClientServices.AsNoTracking().FirstAsync(s => s.Id == id, CancellationToken.None);

    /// <summary>Polls the fake relay for a staff mail naming <paramref name="domainName"/> (the alert is published, not invoked).</summary>
    /// <param name="sender">The fake relay.</param>
    /// <param name="domainName">The domain the mail must name.</param>
    /// <param name="attempts">How many 200 ms polls to make before giving up.</param>
    /// <returns>The matching staff mails received.</returns>
    private static async Task<IReadOnlyList<EmailMessage>> WaitForAlertAsync(FakeEmailSender sender, string domainName, int attempts = 50)
    {
        bool Matches(EmailMessage m) => m.To == AdminEmail && m.Subject.Contains(domainName, StringComparison.Ordinal);

        for (var i = 0; i < attempts && !sender.Sent.Any(Matches); i++)
        {
            await Task.Delay(200);
        }

        return sender.Sent.Where(Matches).ToList();
    }

    /// <summary>A service inside the lead time gets one renewal line for exactly its next period, due on its renewal date.</summary>
    [Fact]
    public async Task ServiceInsideLeadTime_IsInvoicedOnce()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var (clientId, service, start) = await SeedServiceAsync(context, DateTimeOffset.UtcNow.AddDays(10));

        var result = await RaiseAsync(bus, clientId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
        var invoice = (await InvoicesOfAsync(context, clientId)).Should().ContainSingle().Subject;
        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
        invoice.DueDate.Should().Be(start);
        invoice.Currency.Should().Be("USD");
        var line = invoice.Items.Should().ContainSingle().Subject;
        line.ClientServiceId.Should().Be(service.Id);
        line.RegisteredDomainId.Should().BeNull();
        line.Description.Should().Be("Renewal: Starter");
        line.Amount.Should().Be(5m);
        line.RenewalPeriodStart.Should().Be(start);
        line.RenewalPeriodEnd.Should().Be(start!.Value.AddMonths(1));
        line.RenewalAppliedAt.Should().BeNull();

        // Raising is not paying: the renewal date only moves when the line is paid and applied.
        (await ReloadServiceAsync(context, service.Id)).NextRenewalAt.Should().Be(start);

        var again = await RaiseAsync(bus, clientId);

        again.IsSuccess.Should().BeTrue();
        again.Value.Should().Be(0);
        (await InvoicesOfAsync(context, clientId)).Should().ContainSingle();
    }

    /// <summary>
    /// A service's renewal line keeps a copy of its product as it is sold now, on the service's cycle, so a
    /// fulfilment plugin told about the paid renewal delivers the same as for the first order.
    /// </summary>
    /// <param name="type">The product's type.</param>
    /// <param name="digital">Whether the snapshot must flag the product as digital.</param>
    [Theory]
    [InlineData(ProductType.SharedHosting, false)]
    [InlineData(ProductType.Digital, true)]
    public async Task ServiceRenewalLine_CarriesTheProductSnapshot(ProductType type, bool digital)
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var sku = $"snap-{Guid.NewGuid():N}";
        var (clientId, service, _) = await SeedServiceAsync(
            context, DateTimeOffset.UtcNow.AddDays(10), BillingCycle.Annual, type: type, sku: sku);

        var result = await RaiseAsync(bus, clientId);

        result.IsSuccess.Should().BeTrue();
        var invoice = (await InvoicesOfAsync(context, clientId)).Should().ContainSingle().Subject;
        var snapshot = invoice.Items.Should().ContainSingle().Which.ProductSnapshot!;
        snapshot.Should().BeEquivalentTo(
            new ProductSnapshot(service.ProductId, BillingCycle.Annual, digital, sku.ToUpperInvariant(), Metadata: null),
            options => options.Excluding(s => s.Metadata));

        // The database normalises the JSON text (spacing), so the metadata is compared by content, not by characters.
        System.Text.Json.JsonDocument.Parse(snapshot.Metadata!).RootElement.GetProperty("tier").GetString().Should().Be("basic");
        invoice.HasDigitalItems.Should().Be(digital);
    }

    /// <summary>A service outside the lead time is not invoiced yet.</summary>
    [Fact]
    public async Task ServiceOutsideLeadTime_IsNotInvoiced()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var (clientId, _, _) = await SeedServiceAsync(context, DateTimeOffset.UtcNow.AddDays(20));

        var result = await RaiseAsync(bus, clientId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        (await InvoicesOfAsync(context, clientId)).Should().BeEmpty();
    }

    /// <summary>AutoRenew off, AutoTerminateEndOfCycle or an open cancellation request all skip.</summary>
    /// <param name="why">Which of the three reasons the service is seeded with.</param>
    [Theory]
    [InlineData("autorenew")]
    [InlineData("terminate")]
    [InlineData("cancel")]
    public async Task Skips(string why)
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var due = DateTimeOffset.UtcNow.AddDays(10);
        var (clientId, service, _) = why switch
        {
            "autorenew" => await SeedServiceAsync(context, due, autoRenew: false),
            _ => await SeedServiceAsync(context, due),
        };

        if (why == "terminate")
        {
            service.ChangeBillingTerms(new ServiceBillingTerms(
                BillingCycle: service.BillingCycle, Quantity: 1, FirstPaymentAmount: 5m,
                RecurringAmount: service.RecurringAmount, NextRenewalAt: service.NextRenewalAt, PaymentMethod: null,
                PromotionCode: null, SubscriptionId: null));
            service.ConfigureAutomation(true, false, null, true, "Leaving");
            await context.SaveChangesAsync(CancellationToken.None);
        }
        else if (why == "cancel")
        {
            context.CancellationRequests.Add(CancellationRequest.Create(service.Id, CancellationType.EndOfBillingPeriod, "Leaving"));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var result = await RaiseAsync(bus, clientId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        (await InvoicesOfAsync(context, clientId)).Should().BeEmpty();
    }

    /// <summary>An open renewal invoice blocks a second one even after staff edit the renewal date.</summary>
    [Fact]
    public async Task OpenInvoice_BlocksAfterDateEdit()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var (clientId, service, start) = await SeedServiceAsync(context, DateTimeOffset.UtcNow.AddDays(10));
        (await RaiseAsync(bus, clientId)).Value.Should().Be(1);

        // Staff move the renewal date a few days on, still inside the lead time: a new period
        // start, so only the "open invoice" half of the duplicate rule can stop a second bill.
        service.ChangeBillingTerms(new ServiceBillingTerms(
            BillingCycle: service.BillingCycle, Quantity: 1, FirstPaymentAmount: 5m,
            RecurringAmount: service.RecurringAmount, NextRenewalAt: start!.Value.AddDays(3), PaymentMethod: null,
            PromotionCode: null, SubscriptionId: null));
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await RaiseAsync(bus, clientId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        (await InvoicesOfAsync(context, clientId)).Should().ContainSingle();
    }

    /// <summary>A cancelled renewal invoice does not block its period.</summary>
    [Fact]
    public async Task CancelledInvoice_IsReRaised()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var (clientId, service, start) = await SeedServiceAsync(context, DateTimeOffset.UtcNow.AddDays(10));
        (await RaiseAsync(bus, clientId)).Value.Should().Be(1);
        var first = await context.Invoices.FirstAsync(i => i.ClientId == clientId, CancellationToken.None);
        first.Cancel();
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await RaiseAsync(bus, clientId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
        var invoices = await InvoicesOfAsync(context, clientId);
        invoices.Should().HaveCount(2);
        invoices[0].Status.Should().Be(InvoiceStatus.Cancelled);
        invoices[1].Status.Should().Be(InvoiceStatus.Unpaid);
        var line = invoices[1].Items.Should().ContainSingle().Subject;
        line.ClientServiceId.Should().Be(service.Id);
        line.RenewalPeriodStart.Should().Be(start);
    }

    /// <summary>SeparateInvoices gives one invoice per line.</summary>
    [Fact]
    public async Task SeparateInvoices_OnePerLine()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var client = await SeedClientAsync(context);

        // The same Client method UpdateClientHandler calls for the admin's "separate invoices" switch.
        client.UpdateSettings(
            overdueNotices: true, taxExempt: false, separateInvoices: true,
            disableCcProcessing: false, marketingOptIn: false, statusUpdate: true, allowSso: false);
        await context.SaveChangesAsync(CancellationToken.None);
        var (_, first, _) = await SeedServiceAsync(context, DateTimeOffset.UtcNow.AddDays(5), clientId: client.Id);
        var (_, second, _) = await SeedServiceAsync(context, DateTimeOffset.UtcNow.AddDays(9), clientId: client.Id);

        var result = await RaiseAsync(bus, client.Id);

        result.Value.Should().Be(2);
        var invoices = await InvoicesOfAsync(context, client.Id);
        invoices.Should().HaveCount(2);
        invoices.Should().AllSatisfy(i => i.Items.Should().ContainSingle());
        invoices.SelectMany(i => i.Items).Select(l => l.ClientServiceId).Should().BeEquivalentTo(new int?[] { first.Id, second.Id });

        // Each invoice is due on its own line's period start, not the earliest of the client's.
        invoices.Should().AllSatisfy(i => i.DueDate.Should().Be(i.Items.Single().RenewalPeriodStart!.Value));
    }

    /// <summary>Without SeparateInvoices, every due line of a client goes on one invoice due on the earliest period start.</summary>
    [Fact]
    public async Task OneInvoicePerClient_DueOnEarliestStart()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var (clientId, _, earliest) = await SeedServiceAsync(context, DateTimeOffset.UtcNow.AddDays(5));
        await SeedServiceAsync(context, DateTimeOffset.UtcNow.AddDays(9), clientId: clientId);

        var result = await RaiseAsync(bus, clientId);

        result.Value.Should().Be(1);
        var invoice = (await InvoicesOfAsync(context, clientId)).Should().ContainSingle().Subject;
        invoice.Items.Should().HaveCount(2);
        invoice.DueDate.Should().Be(earliest);
    }

    /// <summary>
    /// A period that already started (a back-dated renewal date) is not born overdue: the invoice is
    /// due after the normal payment window, now plus the lead time (14 days by default).
    /// </summary>
    [Fact]
    public async Task BackDatedStart_DueAfterTheLeadTime()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var (clientId, _, start) = await SeedServiceAsync(context, DateTimeOffset.UtcNow.AddDays(-5));

        var before = DateTimeOffset.UtcNow;
        var result = await RaiseAsync(bus, clientId);
        var after = DateTimeOffset.UtcNow;

        result.Value.Should().Be(1);
        var invoice = (await InvoicesOfAsync(context, clientId)).Should().ContainSingle().Subject;
        invoice.Items.Single().RenewalPeriodStart.Should().Be(start);

        // One second either side: the handler's own clock sits between the two reads, and Postgres
        // truncates to microseconds.
        invoice.DueDate.Should().BeOnOrAfter(before.AddDays(14).AddSeconds(-1))
            .And.BeOnOrBefore(after.AddDays(14).AddSeconds(1));
    }

    /// <summary>
    /// A back-dated line sharing an invoice with a line that starts in three days is capped at that
    /// future start, so the back-dated one never pushes the other past its own period.
    /// </summary>
    [Fact]
    public async Task BackDatedAndFutureStart_DueOnTheFutureStart()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var (clientId, _, _) = await SeedServiceAsync(context, DateTimeOffset.UtcNow.AddDays(-5));
        var (_, _, future) = await SeedServiceAsync(context, DateTimeOffset.UtcNow.AddDays(3), clientId: clientId);

        var result = await RaiseAsync(bus, clientId);

        result.Value.Should().Be(1);
        var invoice = (await InvoicesOfAsync(context, clientId)).Should().ContainSingle().Subject;
        invoice.Items.Should().HaveCount(2);
        invoice.DueDate.Should().Be(future);
    }

    /// <summary>A cancelled domain renewal invoice does not block its period, and the open one raised after it does.</summary>
    [Fact]
    public async Task CancelledDomainInvoice_IsReRaisedOnceOnly()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var client = await SeedClientAsync(context);
        var (domain, expiry) = await SeedDomainAsync(context, client.Id, DateTimeOffset.UtcNow.AddDays(10), new() { [1] = 12m });
        (await RaiseAsync(bus, client.Id)).Value.Should().Be(1);
        var first = await context.Invoices.FirstAsync(i => i.ClientId == client.Id, CancellationToken.None);
        first.Cancel();
        await context.SaveChangesAsync(CancellationToken.None);

        (await RaiseAsync(bus, client.Id)).Value.Should().Be(1);
        (await RaiseAsync(bus, client.Id)).Value.Should().Be(0);

        var invoices = await InvoicesOfAsync(context, client.Id);
        invoices.Should().HaveCount(2);
        invoices[0].Status.Should().Be(InvoiceStatus.Cancelled);
        invoices[1].Status.Should().Be(InvoiceStatus.Unpaid);
        var line = invoices[1].Items.Should().ContainSingle().Subject;
        line.RegisteredDomainId.Should().Be(domain.Id);
        line.RenewalPeriodStart.Should().Be(expiry);
    }

    /// <summary>A domain inside the lead time is priced from its TLD's renew price in the payer currency.</summary>
    [Fact]
    public async Task Domain_PricedFromTld()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var client = await SeedClientAsync(context);
        var (domain, expiry) = await SeedDomainAsync(context, client.Id, DateTimeOffset.UtcNow.AddDays(10), new() { [1] = 12m });

        var result = await RaiseAsync(bus, client.Id);

        result.Value.Should().Be(1);
        var invoice = (await InvoicesOfAsync(context, client.Id)).Should().ContainSingle().Subject;
        invoice.Currency.Should().Be("USD");
        invoice.DueDate.Should().Be(expiry);
        var line = invoice.Items.Should().ContainSingle().Subject;
        line.RegisteredDomainId.Should().Be(domain.Id);
        line.ClientServiceId.Should().BeNull();
        line.Amount.Should().Be(12m);
        line.RenewalPeriodStart.Should().Be(expiry);
        line.RenewalPeriodEnd.Should().Be(expiry.AddYears(1));

        // Nothing is renewed by raising the invoice.
        (await context.Domains.AsNoTracking().FirstAsync(d => d.Id == domain.Id)).ExpiresAt.Should().Be(expiry);

        (await RaiseAsync(bus, client.Id)).Value.Should().Be(0);
    }

    /// <summary>
    /// A domain whose TLD has no renew price is skipped, and staff are told on a checkpoint day
    /// (here 7 days left, a default reminder day).
    /// </summary>
    [Fact]
    public async Task Domain_NoPrice_AtCheckpoint_SkippedAndAlerted()
    {
        var sender = new FakeEmailSender();
        using var scope = Host(sender).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var client = await SeedClientAsync(context);

        // The TLD is priced only for two years; the domain renews for one.
        var (domain, _) = await SeedDomainAsync(context, client.Id, DateTimeOffset.UtcNow.AddDays(7).AddHours(-1), new() { [2] = 20m });

        var result = await RaiseAsync(bus, client.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        (await InvoicesOfAsync(context, client.Id)).Should().BeEmpty();
        var mails = await WaitForAlertAsync(sender, domain.Name);
        mails.Should().ContainSingle();
        mails[0].Body.Should().Contain("No renew price");
    }

    /// <summary>
    /// Off a checkpoint day (5 days left: not the lead time, not a reminder day, not expiry day) the
    /// unpriced domain is skipped without a mail; the skip is still logged at Warning.
    /// </summary>
    [Fact]
    public async Task Domain_NoPrice_OffCheckpoint_SkippedWithoutAlert()
    {
        var sender = new FakeEmailSender();
        using var scope = Host(sender).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var client = await SeedClientAsync(context);
        var (domain, _) = await SeedDomainAsync(context, client.Id, DateTimeOffset.UtcNow.AddDays(5).AddHours(-1), new() { [2] = 20m });

        var result = await RaiseAsync(bus, client.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
        (await InvoicesOfAsync(context, client.Id)).Should().BeEmpty();

        // A published alert lands within a second or two; ten polls (2 s) is the window it would use.
        (await WaitForAlertAsync(sender, domain.Name, attempts: 10)).Should().BeEmpty();
    }

    /// <summary>
    /// A domain already inside the window when the feature goes live (10 days left, no checkpoint)
    /// gets no mail that day, and still gets one when it reaches the next checkpoint (7 days left).
    /// </summary>
    [Fact]
    public async Task Domain_NoPrice_InsideWindowAtGoLive_AlertedAtNextCheckpoint()
    {
        var sender = new FakeEmailSender();
        using var scope = Host(sender).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var client = await SeedClientAsync(context);
        var (domain, _) = await SeedDomainAsync(context, client.Id, DateTimeOffset.UtcNow.AddDays(10).AddHours(-1), new() { [2] = 20m });

        (await RaiseAsync(bus, client.Id)).Value.Should().Be(0);
        (await WaitForAlertAsync(sender, domain.Name, attempts: 10)).Should().BeEmpty();

        // Three days later, as far as the domain is concerned: its expiry is now 7 days out.
        var later = DateTimeOffset.UtcNow.AddDays(7).AddHours(-1);
        await context.Domains.Where(d => d.Id == domain.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(d => d.ExpiresAt, later), CancellationToken.None);

        (await RaiseAsync(bus, client.Id)).Value.Should().Be(0);
        (await WaitForAlertAsync(sender, domain.Name)).Should().ContainSingle();
    }

    /// <summary>A sell price in another currency is converted at the stored rates and rounded to the payer's decimals.</summary>
    [Fact]
    public async Task Domain_SellCurrencyConvertedToPayer()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var client = await SeedClientAsync(context);

        // AMD is seeded at 0.00256410 USD: 5000 AMD = 12.8205 USD, rounded to 12.82.
        await SeedDomainAsync(context, client.Id, DateTimeOffset.UtcNow.AddDays(10), new() { [1] = 5000m }, sellCurrency: "AMD");

        (await RaiseAsync(bus, client.Id)).Value.Should().Be(1);

        var invoice = (await InvoicesOfAsync(context, client.Id)).Should().ContainSingle().Subject;
        invoice.Currency.Should().Be("USD");
        invoice.Items.Should().ContainSingle().Which.Amount.Should().Be(12.82m);
    }

    /// <summary>A sell currency with no currency row cannot be converted: the domain is skipped and staff are told why.</summary>
    [Fact]
    public async Task Domain_MissingCurrencyRow_Skipped()
    {
        var sender = new FakeEmailSender();
        using var scope = Host(sender).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var client = await SeedClientAsync(context);
        var (domain, _) = await SeedDomainAsync(
            context, client.Id, DateTimeOffset.UtcNow.AddDays(7).AddHours(-1), new() { [1] = 12m }, sellCurrency: "XYZ");

        var result = await RaiseAsync(bus, client.Id);

        result.Value.Should().Be(0);
        (await InvoicesOfAsync(context, client.Id)).Should().BeEmpty();
        var mails = await WaitForAlertAsync(sender, domain.Name);
        mails.Should().ContainSingle();
        mails[0].Body.Should().Contain("No exchange rate for XYZ");
    }

    /// <summary>A registration period of 0 is priced and invoiced as one year.</summary>
    [Fact]
    public async Task Domain_RegistrationPeriodZero_InvoicedAsOneYear()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var client = await SeedClientAsync(context);
        var (domain, expiry) = await SeedDomainAsync(
            context, client.Id, DateTimeOffset.UtcNow.AddDays(10), new() { [1] = 12m }, registrationPeriod: 0);

        (await RaiseAsync(bus, client.Id)).Value.Should().Be(1);

        var line = (await InvoicesOfAsync(context, client.Id)).Single().Items.Should().ContainSingle().Subject;
        line.RegisteredDomainId.Should().Be(domain.Id);
        line.Amount.Should().Be(12m);
        line.RenewalPeriodEnd.Should().Be(expiry.AddYears(1));
    }

    /// <summary>
    /// A paid line whose renewal is not applied yet (a registrar claim left for staff) blocks a new
    /// invoice even after staff edit the expiry: that term is already paid for.
    /// </summary>
    [Fact]
    public async Task PaidUnappliedLine_BlocksAfterExpiryEdit()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var client = await SeedClientAsync(context);
        var (domain, expiry) = await SeedDomainAsync(context, client.Id, DateTimeOffset.UtcNow.AddDays(10), new() { [1] = 12m });
        var paid = Invoice.Create(client.Id, expiry, "USD", DateTimeOffset.UtcNow);
        paid.AddDomainRenewal($"{domain.Name} renewal", 12m, domain.Id, expiry, expiry.AddYears(1));
        context.Invoices.Add(paid);
        await context.SaveChangesAsync(CancellationToken.None);
        paid.RecordPaidHistory("t", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        // Staff correct the expiry by two days, still inside the window: a new period start.
        var edited = expiry.AddDays(2);
        await context.Domains.Where(d => d.Id == domain.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(d => d.ExpiresAt, edited), CancellationToken.None);

        var result = await RaiseAsync(bus, client.Id);

        result.Value.Should().Be(0);
        (await InvoicesOfAsync(context, client.Id)).Should().ContainSingle();
    }

    /// <summary>A paid invoice whose line for this exact period was applied blocks the period.</summary>
    [Fact]
    public async Task PaidSamePeriod_Blocks()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var (clientId, service, start) = await SeedServiceAsync(context, DateTimeOffset.UtcNow.AddDays(10));
        var paid = Invoice.Create(clientId, start!.Value, "USD", DateTimeOffset.UtcNow);
        paid.AddServiceRenewal("Starter renewal", 5m, service.Id, start!.Value, start!.Value.AddMonths(1));
        context.Invoices.Add(paid);
        await context.SaveChangesAsync(CancellationToken.None);
        paid.RecordPaidHistory("t", DateTimeOffset.UtcNow);
        paid.Items.Single().MarkRenewalApplied(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        (await RaiseAsync(bus, clientId)).Value.Should().Be(0);
        (await InvoicesOfAsync(context, clientId)).Should().ContainSingle();
    }

    /// <summary>A refunded invoice for this exact period blocks it: a refunded term is re-billed only by hand.</summary>
    [Fact]
    public async Task RefundedSamePeriod_Blocks()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var (clientId, service, start) = await SeedServiceAsync(context, DateTimeOffset.UtcNow.AddDays(10));
        var refunded = Invoice.Create(clientId, start!.Value, "USD", DateTimeOffset.UtcNow);
        refunded.AddServiceRenewal("Starter renewal", 5m, service.Id, start!.Value, start!.Value.AddMonths(1));
        context.Invoices.Add(refunded);
        await context.SaveChangesAsync(CancellationToken.None);
        refunded.RecordPaidHistory("t", DateTimeOffset.UtcNow);
        refunded.Refund();
        await context.SaveChangesAsync(CancellationToken.None);

        (await RaiseAsync(bus, clientId)).Value.Should().Be(0);
        var invoice = (await InvoicesOfAsync(context, clientId)).Should().ContainSingle().Subject;
        invoice.Status.Should().Be(InvoiceStatus.Refunded);
    }

    /// <summary>A service with a negative price is skipped; the client's other renewals are still raised.</summary>
    [Fact]
    public async Task NegativeAmountService_Skipped_OthersInvoiced()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var (clientId, _, _) = await SeedServiceAsync(context, DateTimeOffset.UtcNow.AddDays(5), recurringAmount: -1m);
        var (_, good, _) = await SeedServiceAsync(context, DateTimeOffset.UtcNow.AddDays(9), clientId: clientId);

        var result = await RaiseAsync(bus, clientId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
        var invoice = (await InvoicesOfAsync(context, clientId)).Should().ContainSingle().Subject;
        invoice.Items.Should().ContainSingle().Which.ClientServiceId.Should().Be(good.Id);
    }

    /// <summary>Credit pays part of the invoice and the balance goes down.</summary>
    [Fact]
    public async Task Credit_Partial()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var client = await SeedClientAsync(context);
        client.AddCredit(2m);
        await context.SaveChangesAsync(CancellationToken.None);
        await SeedServiceAsync(context, DateTimeOffset.UtcNow.AddDays(10), clientId: client.Id);

        (await RaiseAsync(bus, client.Id)).Value.Should().Be(1);

        var invoice = (await InvoicesOfAsync(context, client.Id)).Should().ContainSingle().Subject;
        invoice.Credit.Should().Be(2m);
        invoice.Total.Should().Be(3m);
        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
        (await context.Clients.AsNoTracking().FirstAsync(c => c.Id == client.Id)).CreditBalance.Should().Be(0m);
    }

    /// <summary>Enough credit pays the invoice, writes no ledger row, and the renewal is applied.</summary>
    [Fact]
    public async Task Credit_Full_PaysAndApplies()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var client = await SeedClientAsync(context);
        client.AddCredit(10m);
        await context.SaveChangesAsync(CancellationToken.None);
        var (_, service, start) = await SeedServiceAsync(context, DateTimeOffset.UtcNow.AddDays(10), clientId: client.Id);

        (await RaiseAsync(bus, client.Id)).Value.Should().Be(1);

        var invoice = (await InvoicesOfAsync(context, client.Id)).Should().ContainSingle().Subject;
        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.Credit.Should().Be(5m);
        invoice.Total.Should().Be(0m);
        invoice.IsPaidInFull.Should().BeTrue();
        (await context.Clients.AsNoTracking().FirstAsync(c => c.Id == client.Id)).CreditBalance.Should().Be(5m);

        // The money was booked when the credit was added; paying with it books nothing again.
        (await context.Transactions.AsNoTracking().AnyAsync(t => t.InvoiceId == invoice.Id)).Should().BeFalse();

        // The fast path is suppressed on this host, so the command the payment event (or the daily
        // sweep) would send is sent here.
        var applied = await bus.InvokeAsync<Result>(new ApplyPaidRenewalsCommand(invoice.Id), CancellationToken.None);

        applied.IsSuccess.Should().BeTrue();
        (await ReloadServiceAsync(context, service.Id)).NextRenewalAt.Should().Be(start!.Value.AddMonths(1));
    }

    /// <summary>With <c>auto_apply_credit</c> off the credit balance is left alone.</summary>
    [Fact]
    public async Task AutoApplyCreditOff_LeavesCredit()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var client = await SeedClientAsync(context);
        client.AddCredit(10m);
        await context.SaveChangesAsync(CancellationToken.None);
        await SeedServiceAsync(context, DateTimeOffset.UtcNow.AddDays(10), clientId: client.Id);

        var row = await context.Settings.FirstAsync(s => s.Key == BillingSettingKeys.AutoApplyCredit);
        row.UpdateValue("false");
        await context.SaveChangesAsync(CancellationToken.None);
        try
        {
            (await RaiseAsync(bus, client.Id)).Value.Should().Be(1);

            var invoice = (await InvoicesOfAsync(context, client.Id)).Should().ContainSingle().Subject;
            invoice.Credit.Should().Be(0m);
            invoice.Total.Should().Be(5m);
            invoice.Status.Should().Be(InvoiceStatus.Unpaid);
            (await context.Clients.AsNoTracking().FirstAsync(c => c.Id == client.Id)).CreditBalance.Should().Be(10m);
        }
        finally
        {
            row.UpdateValue("true");
            await context.SaveChangesAsync(CancellationToken.None);
        }
    }

    /// <summary>An unknown client is refused with <c>CLIENT_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task UnknownClient_IsRefused()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await RaiseAsync(bus, int.MaxValue);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("CLIENT_NOT_FOUND");
    }

    /// <summary>
    /// A client renew order for the domain (its line carries no domain id or period) blocks the
    /// auto-renewal while it still bills the domain (its invoice unpaid, or paid with the order not
    /// fulfilled yet) and stops blocking once the order is fulfilled. Without the block the renew
    /// order's payment and the auto-renewal would each buy the same year.
    /// </summary>
    [Fact]
    public async Task OpenRenewOrder_BlocksTheDomainUntilFulfilled()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var client = await SeedClientAsync(context);
        var (domain, _) = await SeedDomainAsync(context, client.Id, DateTimeOffset.UtcNow.AddDays(10), new() { [1] = 12m });

        // What PlaceOrderHandler leaves behind for a renew order: a plain invoice line, an order
        // item naming the domain, the order still Pending and linked to its invoice.
        var orderInvoice = Invoice.Create(client.Id, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
        orderInvoice.AddItem($"Domain renewal: {domain.Name}", 12m, 1);
        context.Invoices.Add(orderInvoice);
        await context.SaveChangesAsync(CancellationToken.None);
        var order = Order.Create($"ORD-{Guid.NewGuid():N}"[..20], client.Id, "bank-transfer", null, "USD", DateTimeOffset.UtcNow);
        order.AddItem(
            await DomainsTestSeeds.SeedHostingProductAsync(context), "Domain", BillingCycle.Annual, 12m, 12m, domain.Name, null, DomainAction.Renew, null, 1);
        order.LinkInvoice(orderInvoice.Id);
        context.Orders.Add(order);
        await context.SaveChangesAsync(CancellationToken.None);

        (await RaiseAsync(bus, client.Id)).Value.Should().Be(0, "the renew order's unpaid invoice bills the domain");

        orderInvoice.RecordPaidHistory("t", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);
        (await RaiseAsync(bus, client.Id)).Value.Should().Be(0, "paid but not fulfilled: the registrar renewal is still pending");

        // Fulfilled. The expiry is left where it was on purpose, so the only thing that changed is
        // the order's state: normal behaviour resumes and the period is invoiced.
        order.Accept(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);
        (await RaiseAsync(bus, client.Id)).Value.Should().Be(1);
        (await InvoicesOfAsync(context, client.Id)).Should().Contain(i => i.Items.Any(l => l.RegisteredDomainId == domain.Id));
    }

    /// <summary>A cancelled renew order does not block the auto-renewal.</summary>
    [Fact]
    public async Task CancelledRenewOrder_DoesNotBlock()
    {
        using var scope = Host(new FakeEmailSender()).Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var client = await SeedClientAsync(context);
        var (domain, _) = await SeedDomainAsync(context, client.Id, DateTimeOffset.UtcNow.AddDays(10), new() { [1] = 12m });

        var orderInvoice = Invoice.Create(client.Id, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
        orderInvoice.AddItem($"Domain renewal: {domain.Name}", 12m, 1);
        orderInvoice.Cancel();
        context.Invoices.Add(orderInvoice);
        await context.SaveChangesAsync(CancellationToken.None);
        var order = Order.Create($"ORD-{Guid.NewGuid():N}"[..20], client.Id, "bank-transfer", null, "USD", DateTimeOffset.UtcNow);
        order.AddItem(
            await DomainsTestSeeds.SeedHostingProductAsync(context), "Domain", BillingCycle.Annual, 12m, 12m, domain.Name, null, DomainAction.Renew, null, 1);
        order.LinkInvoice(orderInvoice.Id);
        order.Cancel();
        context.Orders.Add(order);
        await context.SaveChangesAsync(CancellationToken.None);

        (await RaiseAsync(bus, client.Id)).Value.Should().Be(1);
    }
}
