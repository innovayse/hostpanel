using Innovayse.Hostpanel.Application.Billing.Commands.ReconcileGatewayPaymentsCron;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Hostpanel.Sdk.Plugins.Enums;
using Hostpanel.Sdk.Plugins.Payment;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// A hosted card session that is still open when the client chooses a manual bank transfer must never lose its money. The session
/// stays on the invoice, so a card payment finished after the choice is found by the reconciler: it settles the invoice and ends the
/// transfer wait, however old the session is; and when the transfer was confirmed first, whatever the session took later goes to the
/// client's balance, also for a session older than the stalled threshold.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class BankTransferGatewaySessionTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The line price of every invoice seeded here, in the invoice currency (minor units: 1000).</summary>
    private const decimal Price = 10m;

    /// <summary>Builds a host whose clock the test moves and whose payment plugins come from <paramref name="resolver"/>.</summary>
    /// <param name="resolver">The resolver the host resolves plugins through.</param>
    /// <param name="clock">The clock the host reads the time from.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(FakePaymentPluginResolver resolver, ManualTimeProvider clock) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IPaymentPluginResolver>(resolver);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);

                // Seeding saves must not publish their own events: the test reads the state the reconciler leaves.
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(new FakeEmailSender());
                services.RemoveAll<IDomainEventGate>();
                services.AddScoped<IDomainEventGate, SuppressedDomainEventGate>();
            }));

    /// <summary>
    /// Seeds a client and an invoice that holds a hosted session started <paramref name="sessionAge"/> ago and a bank transfer
    /// the client chose after it.
    /// </summary>
    /// <param name="host">The host whose database is seeded.</param>
    /// <param name="module">This test's own plugin module id.</param>
    /// <param name="now">The moment the test treats as the present.</param>
    /// <param name="sessionAge">How long ago the hosted session was started.</param>
    /// <param name="confirmTransfer">Whether staff then confirm the transfer, which settles the invoice.</param>
    /// <returns>The invoice and client ids.</returns>
    private static async Task<(int InvoiceId, int ClientId)> SeedTransferWithSessionAsync(
        WebApplicationFactory<Program> host, string module, DateTimeOffset now, TimeSpan sessionAge, bool confirmTransfer)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = Client.Create($"subject-{Guid.NewGuid():N}", "Bank", "Payer", $"{Guid.NewGuid():N}@example.test", now);
        db.Clients.Add(client);
        await db.SaveChangesAsync(CancellationToken.None);

        var invoice = Invoice.Create(client.Id, now.AddDays(7), "USD", now);
        invoice.AddItem("Hosting", Price, 1);
        invoice.SetGatewaySession(module, $"gw-{Guid.NewGuid():N}", now - sessionAge);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(CancellationToken.None);

        // The client chose the transfer after the session's payment window had passed, which is allowed.
        invoice.RequestBankTransfer(now - sessionAge + Invoice.GatewaySessionWindow + TimeSpan.FromMinutes(1));
        await db.SaveChangesAsync(CancellationToken.None);

        if (confirmTransfer)
        {
            db.Transactions.Add(invoice.ConfirmBankTransfer(now)!);
            await db.SaveChangesAsync(CancellationToken.None);
        }

        return (invoice.Id, client.Id);
    }

    /// <summary>Runs one reconciliation pass on the host.</summary>
    /// <param name="host">The host to run it on.</param>
    /// <returns>A task that completes once the pass has ended.</returns>
    private static async Task ReconcileAsync(WebApplicationFactory<Program> host)
    {
        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync(new ReconcileGatewayPaymentsCronCommand(), CancellationToken.None);
    }

    /// <summary>Reads an invoice with its payments.</summary>
    /// <param name="host">The host whose database is read.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The invoice.</returns>
    private static async Task<Invoice> LoadAsync(WebApplicationFactory<Program> host, int invoiceId)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Invoices.AsNoTracking().Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId);
    }

    /// <summary>
    /// A card payment finished after the window, while the invoice waits for a transfer, settles the invoice with the gateway's money
    /// and ends the wait; nothing goes to the balance, and the money is recorded once.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    public async Task ACardPaymentFinishedWhileATransferWaits_SettlesTheInvoiceAndEndsTheWait(int sessionAgeHours)
    {
        var module = $"bt-session-{Guid.NewGuid():N}";
        var reference = $"ref-{Guid.NewGuid():N}";
        var plugin = new FakePaymentPlugin { StatusResult = new(GatewayPaymentState.Paid, reference, "orderStatus:2", AmountMinor: 1000) };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(module, plugin);
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        using var host = BuildHost(resolver, clock);
        var (invoiceId, clientId) = await SeedTransferWithSessionAsync(host, module, clock.GetUtcNow(), TimeSpan.FromHours(sessionAgeHours), confirmTransfer: false);

        await ReconcileAsync(host);
        await ReconcileAsync(host);

        var invoice = await LoadAsync(host, invoiceId);
        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.BankTransferRequestedAt.Should().BeNull();
        invoice.Transactions.Should().ContainSingle(t => t.Type == InvoiceTransactionType.Payment && t.Gateway == module && t.TransactionId == reference);
        (await BillingTestSeeds.CreditBalanceOfAsync(host, clientId)).Should().Be(0m);
    }

    /// <summary>A card session that is still pending leaves the waiting transfer alone.</summary>
    [Fact]
    public async Task APendingCardSession_LeavesTheWaitingTransferAlone()
    {
        var module = $"bt-session-{Guid.NewGuid():N}";
        var plugin = new FakePaymentPlugin();
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(module, plugin);
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        using var host = BuildHost(resolver, clock);
        var (invoiceId, _) = await SeedTransferWithSessionAsync(host, module, clock.GetUtcNow(), TimeSpan.FromHours(2), confirmTransfer: false);

        await ReconcileAsync(host);

        plugin.GetStatusCallCount.Should().Be(1, "the waiting transfer still holds a session the payer may finish");
        var invoice = await LoadAsync(host, invoiceId);
        invoice.Status.Should().Be(InvoiceStatus.PaymentPending);
        invoice.BankTransferRequestedAt.Should().NotBeNull();
    }

    /// <summary>
    /// Staff confirm the transfer more than a day after the session started; the session then reports paid. The money goes to the
    /// client's balance once, because the invoice was already settled by the transfer.
    /// </summary>
    [Fact]
    public async Task ATransferConfirmedAfterADay_StillCreditsALateCardPayment()
    {
        var module = $"bt-session-{Guid.NewGuid():N}";
        var reference = $"ref-{Guid.NewGuid():N}";
        var plugin = new FakePaymentPlugin { StatusResult = new(GatewayPaymentState.Paid, reference, "orderStatus:2", AmountMinor: 1000) };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(module, plugin);
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        using var host = BuildHost(resolver, clock);
        var (invoiceId, clientId) = await SeedTransferWithSessionAsync(host, module, clock.GetUtcNow(), TimeSpan.FromHours(30), confirmTransfer: true);

        await ReconcileAsync(host);
        await ReconcileAsync(host);

        (await BillingTestSeeds.CreditBalanceOfAsync(host, clientId)).Should().Be(Price);
        var invoice = await LoadAsync(host, invoiceId);
        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.Transactions.Should().NotContain(t => t.TransactionId == reference, "the invoice was paid by the transfer; the card money is on the balance");
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Transactions.AsNoTracking().CountAsync(t => t.TransactionId == reference)).Should().Be(1);
    }
}
