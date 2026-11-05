using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>POST /api/billing/{id}/refund</c> against Postgres: full and partial refunds, the cap at what
/// was paid, a gateway refund for money no gateway took, and the unknown-invoice refusal.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RefundInvoicePaymentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The hosted gateway module the gateway-paid invoices in these tests were paid through.</summary>
    private const string HostedGateway = "bank-gateway";

    /// <summary>Creates an invoice of <paramref name="price"/> and pays it in full manually.</summary>
    /// <param name="admin">An admin HTTP client.</param>
    /// <param name="price">The invoice amount.</param>
    /// <returns>The paid invoice id.</returns>
    private async Task<int> PaidInvoiceAsync(HttpClient admin, decimal price)
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, price);
        var paid = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/payment", new
        {
            date = DateTimeOffset.UtcNow,
            gateway = PaymentMethodNames.Manual,
            transactionId = $"pay-{Guid.NewGuid():N}",
            amount = price,
            fees = 0m,
        });
        paid.StatusCode.Should().Be(HttpStatusCode.NoContent);
        return invoiceId;
    }

    /// <summary>Reads an invoice's status name.</summary>
    /// <param name="admin">An admin HTTP client.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The status as it travels on the wire.</returns>
    private static async Task<string?> StatusOfAsync(HttpClient admin, int invoiceId)
    {
        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var body = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("status").GetString();
    }

    /// <summary>A full refund to the credit balance marks the invoice Refunded.</summary>
    [Fact]
    public async Task RefundViaCreditBalanceRefundsAPaidInvoice()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var invoiceId = await PaidInvoiceAsync(admin, 19.99m);

        var refunded = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 19.99m,
            refundType = nameof(RefundType.CreditBalance),
        });

        refunded.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Refunded));
    }

    /// <summary>
    /// A partial manual refund leaves the invoice PartiallyRefunded and writes its client ledger row,
    /// under the refund's own reference, with the status change.
    /// </summary>
    [Fact]
    public async Task PartialRefundLeavesTheInvoicePartiallyRefundedWithItsLedgerRow()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var invoiceId = await PaidInvoiceAsync(admin, 20m);
        var reference = $"refund-{Guid.NewGuid():N}";

        var refunded = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 5m,
            refundType = nameof(RefundType.Manual),
            gateway = PaymentMethodNames.BankTransfer,
            refundTransactionId = reference,
        });

        refunded.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.PartiallyRefunded));
        (await RefundRowsAsync(invoiceId)).Should().ContainSingle()
            .Which.Should().Match<Transaction>(t => t.TransactionId == reference && t.AmountOut == 5m && t.Source == TransactionSource.Manual);
    }

    /// <summary>Reads the client ledger rows that took money out for an invoice, oldest first.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The refund rows on the client ledger.</returns>
    private async Task<List<Transaction>> RefundRowsAsync(int invoiceId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await context.Transactions.AsNoTracking()
            .Where(t => t.InvoiceId == invoiceId && t.AmountOut > 0)
            .OrderBy(t => t.Id)
            .ToListAsync();
    }

    /// <summary>
    /// Two partial refunds in a row are each capped by what was paid less the earlier ones: the
    /// second may not pass the rest, the exact rest is accepted, and only then is the invoice Refunded.
    /// Each accepted refund writes its own ledger row; the refused one writes none.
    /// </summary>
    [Fact]
    public async Task SecondPartialRefundIsCappedByWhatTheFirstLeftAsync()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var invoiceId = await PaidInvoiceAsync(admin, 20m);

        var first = await ManualRefundAsync(admin, invoiceId, 15m);
        var afterFirst = await StatusOfAsync(admin, invoiceId);
        var tooMuch = await ManualRefundAsync(admin, invoiceId, 10m);
        var theRest = await ManualRefundAsync(admin, invoiceId, 5m);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        afterFirst.Should().Be(nameof(InvoiceStatus.PartiallyRefunded));
        tooMuch.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(tooMuch)).Should().Be(HostpanelErrorCodes.INVOICE_REFUND_EXCEEDS_PAID);
        theRest.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Refunded));
        (await RefundRowsAsync(invoiceId)).Select(t => t.AmountOut).Should().Equal(15m, 5m);
    }

    /// <summary>
    /// Two partial refunds in a row that both stay below what was paid each pass the cap and leave the
    /// invoice PartiallyRefunded, with what is left still refundable.
    /// </summary>
    [Fact]
    public async Task TwoPartialRefundsInARowStayPartiallyRefunded()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var invoiceId = await PaidInvoiceAsync(admin, 30m);

        var first = await ManualRefundAsync(admin, invoiceId, 10m);
        var second = await ManualRefundAsync(admin, invoiceId, 12.5m);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.PartiallyRefunded));
        (await RefundRowsAsync(invoiceId)).Select(t => t.AmountOut).Should().Equal(10m, 12.5m);
    }

    /// <summary>Refunds <paramref name="amount"/> of an invoice by hand, under a fresh refund reference.</summary>
    /// <param name="admin">An admin HTTP client.</param>
    /// <param name="invoiceId">The invoice to refund.</param>
    /// <param name="amount">The amount to refund.</param>
    /// <returns>The refund endpoint's response.</returns>
    private static Task<HttpResponseMessage> ManualRefundAsync(HttpClient admin, int invoiceId, decimal amount) =>
        admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount,
            refundType = nameof(RefundType.Manual),
            gateway = PaymentMethodNames.BankTransfer,
            refundTransactionId = $"refund-{Guid.NewGuid():N}",
        });

    /// <summary>A refund above what was paid answers 400 with <c>INVOICE_REFUND_EXCEEDS_PAID</c>.</summary>
    [Fact]
    public async Task RefundAbovePaidIsRefused()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var invoiceId = await PaidInvoiceAsync(admin, 10m);

        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 10.01m,
            refundType = nameof(RefundType.CreditBalance),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_REFUND_EXCEEDS_PAID);
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Paid));
    }

    /// <summary>A gateway refund for money no gateway took answers 400 with <c>INVOICE_NOT_GATEWAY_PAID</c>.</summary>
    [Fact]
    public async Task GatewayRefundOfAManualPaymentIsRefused()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var invoiceId = await PaidInvoiceAsync(admin, 10m);

        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 10m,
            refundType = nameof(RefundType.Gateway),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_GATEWAY_PAID);
    }

    /// <summary>
    /// A gateway refund of a payment recorded as a card payment, with no hosted session that took it,
    /// answers 400 <c>INVOICE_NOT_GATEWAY_PAID</c>: no on-site card gateway exists any more, so such
    /// a payment is refunded by recording a manual refund.
    /// </summary>
    [Fact]
    public async Task GatewayRefundOfALegacyCardPaymentIsRefused()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);

        int invoiceId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
            invoice.AddItem("Hosting", 10m, 1);
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync(CancellationToken.None);
            context.Transactions.Add(invoice.Settle(PaymentMethodNames.Card, $"ch_{Guid.NewGuid():N}", DateTimeOffset.UtcNow, TransactionSource.Manual)!);
            await context.SaveChangesAsync(CancellationToken.None);
            invoiceId = invoice.Id;
        }

        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 4m,
            refundType = nameof(RefundType.Gateway),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_GATEWAY_PAID);
    }

    /// <summary>
    /// An invoice paid 30 by a recorded payment and the rest by a settlement can be refunded in full:
    /// refunding the first 30 leaves it Paid, and the other 70 is still refundable.
    /// </summary>
    [Fact]
    public async Task PartialPaymentThenSettlementIsRefundableInFull()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 100m);
        var paid = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/payment", new
        {
            date = DateTimeOffset.UtcNow,
            gateway = PaymentMethodNames.Manual,
            transactionId = $"pay-{Guid.NewGuid():N}",
            amount = 30m,
            fees = 0m,
        });
        paid.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var settled = await admin.PostAsJsonAsync("/api/billing/bulk", new
        {
            invoiceIds = new[] { invoiceId },
            action = nameof(InvoiceBulkAction.MarkPaid),
        });
        settled.IsSuccessStatusCode.Should().BeTrue();

        var first = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 30m,
            refundType = nameof(RefundType.CreditBalance),
        });

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.PartiallyRefunded));

        var rest = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 70m,
            refundType = nameof(RefundType.CreditBalance),
        });

        rest.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Refunded));
    }

    /// <summary>
    /// Two partial refunds of 60 read the same 100 invoice at once: both pass RefundableAmount, but
    /// each changes the invoice row, so the second save fails the row's concurrency check and only
    /// one refund is recorded.
    /// </summary>
    [Fact]
    public async Task ConcurrentPartialRefundsCannotBothBeSaved()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var invoiceId = await PaidInvoiceAsync(admin, 100m);
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var first = await firstContext.Invoices.Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId);
        var second = await secondContext.Invoices.Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId);
        var now = DateTimeOffset.UtcNow;

        firstContext.Transactions.Add(first.AddRefund(now, PaymentMethodNames.Manual, $"r-{Guid.NewGuid():N}", 60m, 0m, null, false, TransactionSource.Manual));
        secondContext.Transactions.Add(second.AddRefund(now.AddMilliseconds(1), PaymentMethodNames.Manual, $"r-{Guid.NewGuid():N}", 60m, 0m, null, false, TransactionSource.Manual));
        await firstContext.SaveChangesAsync(CancellationToken.None);
        var act = () => secondContext.SaveChangesAsync(CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
        using var readScope = factory.Services.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var saved = await readContext.Invoices.AsNoTracking().Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId);
        saved.AmountRefunded.Should().Be(60m);
        saved.RefundableAmount.Should().Be(40m);
    }

    /// <summary>
    /// A hosted-gateway refund is capped by what that gateway took: on an invoice paid 30 manually
    /// and 70 through the gateway, 80 is refused before the plugin is called and 70 goes through.
    /// </summary>
    [Fact]
    public async Task HostedGatewayRefundIsCappedByThatGatewaysPayments()
    {
        var plugin = new FakePaymentPlugin { RefundResult = "gw-refund-cap" };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(HostedGateway, plugin);
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentPluginResolver>(resolver)));
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await HostedGatewayPaidInvoiceAsync(host, manualPart: 30m);

        var tooMuch = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 80m,
            refundType = nameof(RefundType.Gateway),
        });

        tooMuch.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(tooMuch)).Should().Be(HostpanelErrorCodes.INVOICE_REFUND_EXCEEDS_PAID);
        plugin.LastRefundGatewayOrderId.Should().BeNull();

        var gatewayPart = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 70m,
            refundType = nameof(RefundType.Gateway),
        });

        gatewayPart.StatusCode.Should().Be(HttpStatusCode.NoContent);
        plugin.LastRefundAmountMinor.Should().Be(7000);
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.PartiallyRefunded));
    }

    /// <summary>
    /// A hosted gateway whose refund call throws answers 502 with <c>GATEWAY_REFUND_REFUSED</c> and
    /// records nothing; its outcome is unknown, so the claim stays and a retry is a 409 conflict
    /// rather than a second blind refund.
    /// </summary>
    [Fact]
    public async Task HostedGatewayThatThrowsIsARefusalAndKeepsTheClaim()
    {
        // No RefundResult: the fake throws on RefundAsync, as a failing gateway would.
        var plugin = new FakePaymentPlugin();
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(HostedGateway, plugin);
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentPluginResolver>(resolver)));
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await HostedGatewayPaidInvoiceAsync(host, manualPart: 0m);

        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 10m,
            refundType = nameof(RefundType.Gateway),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.GATEWAY_REFUND_REFUSED);
        using (var readScope = host.Services.CreateScope())
        {
            var readContext = readScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            (await readContext.Transactions.AsNoTracking().AnyAsync(t => t.InvoiceId == invoiceId && t.AmountOut > 0))
                .Should().BeFalse();
        }

        var retry = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 10m,
            refundType = nameof(RefundType.Gateway),
        });

        retry.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(retry)).Should().Be(HostpanelErrorCodes.INVOICE_REFUND_CONFLICT);
    }

    /// <summary>
    /// The host sends the plugin a refund id with the claim, and a retry of the same refund after the claim's lease has run
    /// out (the first call threw, so its outcome was unknown) sends the same id, so a gateway that keys on it refunds once.
    /// </summary>
    [Fact]
    public async Task RetryOfTheSameGatewayRefund_ReusesTheRefundId()
    {
        var plugin = new FakePaymentPlugin();
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(HostedGateway, plugin);
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentPluginResolver>(resolver)));
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await HostedGatewayPaidInvoiceAsync(host, manualPart: 0m);

        var first = await GatewayRefundAsync(admin, invoiceId, 10m);
        first.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        await ExpireRefundClaimAsync(host, invoiceId);
        plugin.RefundResult = "gw-refund-retry";
        var retry = await GatewayRefundAsync(admin, invoiceId, 10m);

        retry.StatusCode.Should().Be(HttpStatusCode.NoContent);
        plugin.RefundInstructions.Should().HaveCount(2);
        plugin.RefundInstructions[0].RefundId.Should().NotBeNullOrWhiteSpace();
        plugin.RefundInstructions[1].RefundId.Should().Be(plugin.RefundInstructions[0].RefundId);
        plugin.RefundInstructions[1].AmountMinor.Should().Be(plugin.RefundInstructions[0].AmountMinor);
    }

    /// <summary>A retry for another amount is another refund and gets its own id.</summary>
    [Fact]
    public async Task RetryWithAnotherAmount_GetsANewRefundId()
    {
        var plugin = new FakePaymentPlugin();
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(HostedGateway, plugin);
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentPluginResolver>(resolver)));
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await HostedGatewayPaidInvoiceAsync(host, manualPart: 0m);

        (await GatewayRefundAsync(admin, invoiceId, 10m)).StatusCode.Should().Be(HttpStatusCode.BadGateway);
        await ExpireRefundClaimAsync(host, invoiceId);
        plugin.RefundResult = "gw-refund-other";
        (await GatewayRefundAsync(admin, invoiceId, 12m)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        plugin.RefundInstructions.Should().HaveCount(2);
        plugin.RefundInstructions[1].RefundId.Should().NotBe(plugin.RefundInstructions[0].RefundId);
    }

    /// <summary>Once a refund is recorded its id is spent: the next refund, even of the same amount, gets a new one.</summary>
    [Fact]
    public async Task ARecordedRefund_EndsItsId_SoTheNextRefundGetsAnother()
    {
        var plugin = new FakePaymentPlugin { RefundResult = "gw-refund-ok" };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(HostedGateway, plugin);
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentPluginResolver>(resolver)));
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await HostedGatewayPaidInvoiceAsync(host, manualPart: 0m);

        (await GatewayRefundAsync(admin, invoiceId, 10m)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        plugin.RefundResult = "gw-refund-ok-2";
        (await GatewayRefundAsync(admin, invoiceId, 10m)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        plugin.RefundInstructions.Should().HaveCount(2);
        plugin.RefundInstructions[1].RefundId.Should().NotBe(plugin.RefundInstructions[0].RefundId);
    }

    /// <summary>The first partial refund of a gateway order tells the plugin nothing was refunded before it.</summary>
    [Fact]
    public async Task FirstPartialGatewayRefund_SendsZeroAlreadyRefunded()
    {
        var plugin = new FakePaymentPlugin { RefundResult = "gw-refund-1" };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(HostedGateway, plugin);
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentPluginResolver>(resolver)));
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await HostedGatewayPaidInvoiceAsync(host, manualPart: 0m);

        (await GatewayRefundAsync(admin, invoiceId, 10m)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        plugin.RefundInstructions.Should().ContainSingle().Which.AlreadyRefundedMinor.Should().Be(0);
    }

    /// <summary>A second, smaller partial refund carries the sum the ledger recorded for the first one.</summary>
    [Fact]
    public async Task SecondSmallerPartialGatewayRefund_SendsTheRecordedSum()
    {
        var plugin = new FakePaymentPlugin { RefundResult = "gw-refund-1" };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(HostedGateway, plugin);
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentPluginResolver>(resolver)));
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await HostedGatewayPaidInvoiceAsync(host, manualPart: 0m);

        (await GatewayRefundAsync(admin, invoiceId, 10m)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        plugin.RefundResult = "gw-refund-2";
        (await GatewayRefundAsync(admin, invoiceId, 4m)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        plugin.RefundInstructions.Should().HaveCount(2);
        plugin.RefundInstructions[1].AmountMinor.Should().Be(400);
        plugin.RefundInstructions[1].AlreadyRefundedMinor.Should().Be(1000);
    }

    /// <summary>A retry of the pending refund sends the same id and the same sum, not counting the pending refund itself.</summary>
    [Fact]
    public async Task RetryOfThePendingGatewayRefund_SendsTheSameAlreadyRefunded()
    {
        var plugin = new FakePaymentPlugin { RefundResult = "gw-refund-1" };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(HostedGateway, plugin);
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentPluginResolver>(resolver)));
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await HostedGatewayPaidInvoiceAsync(host, manualPart: 0m);

        (await GatewayRefundAsync(admin, invoiceId, 10m)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        plugin.RefundResult = null;
        (await GatewayRefundAsync(admin, invoiceId, 4m)).StatusCode.Should().Be(HttpStatusCode.BadGateway);
        await ExpireRefundClaimAsync(host, invoiceId);
        plugin.RefundResult = "gw-refund-2";
        (await GatewayRefundAsync(admin, invoiceId, 4m)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        plugin.RefundInstructions.Should().HaveCount(3);
        plugin.RefundInstructions[2].RefundId.Should().Be(plugin.RefundInstructions[1].RefundId);
        plugin.RefundInstructions[1].AlreadyRefundedMinor.Should().Be(1000);
        plugin.RefundInstructions[2].AlreadyRefundedMinor.Should().Be(1000);
    }

    /// <summary>Asks for a gateway refund of <paramref name="amount"/>.</summary>
    /// <param name="admin">An admin HTTP client.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="amount">The amount asked for.</param>
    /// <returns>The response.</returns>
    private static Task<HttpResponseMessage> GatewayRefundAsync(HttpClient admin, int invoiceId, decimal amount) =>
        admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount,
            refundType = nameof(RefundType.Gateway),
        });

    /// <summary>Moves the start of an invoice's refund claim back past its lease, as if the lease had run out.</summary>
    /// <param name="host">The host whose database holds the invoice.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>A task that completes when the claim is aged.</returns>
    private static async Task ExpireRefundClaimAsync(WebApplicationFactory<Program> host, int invoiceId)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var past = DateTimeOffset.UtcNow.AddHours(-1);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "Invoices" SET "RefundStartedAt" = {past} WHERE "Id" = {invoiceId}""");
    }

    /// <summary>
    /// Seeds a 100 USD invoice paid <paramref name="manualPart"/> by a manual payment and the rest
    /// through the hosted gateway, whose session the settlement keeps.
    /// </summary>
    /// <param name="host">The host whose database is seeded.</param>
    /// <param name="manualPart">The part paid manually first; zero for none.</param>
    /// <returns>The invoice id.</returns>
    private static async Task<int> HostedGatewayPaidInvoiceAsync(WebApplicationFactory<Program> host, decimal manualPart)
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(host);
        var now = DateTimeOffset.UtcNow;

        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = Invoice.Create(clientId, now.AddDays(7), "USD", now);
        invoice.AddItem("Hosting", 100m, 1);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        if (manualPart > 0)
        {
            context.Transactions.Add(invoice.AddPayment(now.AddMinutes(-5), PaymentMethodNames.Manual, $"pay-{Guid.NewGuid():N}", manualPart, 0m, null, TransactionSource.Manual));
        }

        invoice.SetGatewaySession(HostedGateway, $"gw-order-{Guid.NewGuid():N}", now);
        context.Transactions.Add(invoice.Settle(HostedGateway, $"gw-pay-{Guid.NewGuid():N}", now, TransactionSource.Manual)!);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }

    /// <summary>Refunding an id that names no invoice answers 404 with <c>INVOICE_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task RefundRefusesAnUnknownInvoice()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync($"/api/billing/{BillingTestSeeds.NoSuchId}/refund", new
        {
            amount = 1m,
            refundType = nameof(RefundType.Manual),
            gateway = PaymentMethodNames.Manual,
            refundTransactionId = $"refund-{Guid.NewGuid():N}",
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_NOT_FOUND);
    }

    /// <summary>Refunding a payment is not for anonymous callers.</summary>
    [Fact]
    public async Task RefundRefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/billing/1/refund", new
        {
            amount = 1m,
            refundType = nameof(RefundType.Manual),
            gateway = PaymentMethodNames.Manual,
            refundTransactionId = "refund-ref",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
