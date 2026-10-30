using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Billing.Commands.RefundInvoicePayment;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Infrastructure.Time;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

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
            .Which.Should().Match<Transaction>(t => t.TransactionId == reference && t.AmountOut == 5m);
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
    /// A Gateway refund of an invoice paid by saved card goes to the card gateway against the
    /// original charge's reference for the amount asked, writes the refund ledger row, and a
    /// partial amount leaves the invoice Paid.
    /// </summary>
    [Fact]
    public async Task GatewayRefundOfACardPaymentGoesToTheCardGateway()
    {
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true, RefundId = "re_card_1" };
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ICardPaymentGateway>(cardGateway)));
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var clientId = await BillingTestSeeds.SeedClientAsync(host);
        var chargeId = $"ch_{Guid.NewGuid():N}";

        int invoiceId;
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
            invoice.AddItem("Hosting", 10m, 1);
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync(CancellationToken.None);
            var ledgerRow = invoice.Settle(PaymentMethodNames.Card, chargeId, DateTimeOffset.UtcNow);
            context.Transactions.Add(ledgerRow!);
            await context.SaveChangesAsync(CancellationToken.None);
            invoiceId = invoice.Id;
        }

        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 4m,
            refundType = nameof(RefundType.Gateway),
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var instruction = cardGateway.RefundInstructions.Should().ContainSingle().Subject;
        instruction.TransactionId.Should().Be(chargeId);
        instruction.Amount.Should().Be(4m);
        instruction.Currency.Should().Be("USD");
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.PartiallyRefunded));

        using var readScope = host.Services.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var refundRow = await readContext.Transactions.AsNoTracking()
            .SingleAsync(t => t.InvoiceId == invoiceId && t.AmountOut > 0);
        refundRow.TransactionId.Should().Be("re_card_1");
        refundRow.PaymentMethod.Should().Be(PaymentMethodNames.Card);
        refundRow.AmountOut.Should().Be(4m);
    }

    /// <summary>
    /// A card payment that settled an invoice whose hosted session was still open is refunded to the
    /// card: the kept session took none of the money, so the hosted gateway is never asked.
    /// </summary>
    [Fact]
    public async Task GatewayRefundWithAnOpenSessionThatTookNothingGoesToTheCardGateway()
    {
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true, RefundId = "re_card_session" };
        var plugin = new FakePaymentPlugin { RefundResult = "gw-refund-wrong" };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(HostedGateway, plugin);
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<ICardPaymentGateway>(cardGateway);
                services.AddSingleton<IPaymentPluginResolver>(resolver);
            }));
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var clientId = await BillingTestSeeds.SeedClientAsync(host);
        var chargeId = $"ch_{Guid.NewGuid():N}";

        int invoiceId;
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
            invoice.AddItem("Hosting", 10m, 1);
            invoice.SetGatewaySession(HostedGateway, $"gw-order-{Guid.NewGuid():N}", DateTimeOffset.UtcNow);
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync(CancellationToken.None);
            context.Transactions.Add(invoice.Settle(PaymentMethodNames.Card, chargeId, DateTimeOffset.UtcNow)!);
            await context.SaveChangesAsync(CancellationToken.None);
            invoiceId = invoice.Id;
        }

        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 10m,
            refundType = nameof(RefundType.Gateway),
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        cardGateway.RefundInstructions.Should().ContainSingle().Which.TransactionId.Should().Be(chargeId);
        plugin.LastRefundGatewayOrderId.Should().BeNull();
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Refunded));
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
    /// A card refund larger than the newest charge is spread over the invoice's card charges, newest
    /// first, each asked for no more than it took, and every part is on the ledger.
    /// </summary>
    [Fact]
    public async Task GatewayRefundSpreadsOverTheCardChargesNewestFirst()
    {
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true, RefundId = "re_spread" };
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ICardPaymentGateway>(cardGateway)));
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var (invoiceId, olderCharge, newerCharge) = await TwoCardChargesAsync(host);

        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 90m,
            refundType = nameof(RefundType.Gateway),
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        cardGateway.RefundInstructions.Should().Equal(
            new CardRefundInstruction(newerCharge, 60m, "USD", $"refund-{invoiceId}-{newerCharge}-0"),
            new CardRefundInstruction(olderCharge, 30m, "USD", $"refund-{invoiceId}-{olderCharge}-0"));
        using var readScope = host.Services.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var refunds = await readContext.Transactions.AsNoTracking()
            .Where(t => t.InvoiceId == invoiceId && t.AmountOut > 0)
            .Select(t => t.AmountOut)
            .ToListAsync();
        refunds.Should().BeEquivalentTo(new[] { 60m, 30m });
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.PartiallyRefunded));

        var restResponse = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 10m,
            refundType = nameof(RefundType.Gateway),
        });

        restResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        cardGateway.RefundInstructions[^1].Should().Be(new CardRefundInstruction(olderCharge, 10m, "USD", $"refund-{invoiceId}-{olderCharge}-3000"));
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Refunded));
    }

    /// <summary>
    /// A card gateway that refuses the refund answers 502 with <c>CARD_REFUND_REFUSED</c> and nothing
    /// is recorded.
    /// </summary>
    [Fact]
    public async Task RefusedCardRefundIsAFailureNotAnException()
    {
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true, RefusesRefunds = true };
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ICardPaymentGateway>(cardGateway)));
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var (invoiceId, _, _) = await TwoCardChargesAsync(host);

        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 50m,
            refundType = nameof(RefundType.Gateway),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CARD_REFUND_REFUSED);
        using var readScope = host.Services.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await readContext.Transactions.AsNoTracking().AnyAsync(t => t.InvoiceId == invoiceId && t.AmountOut > 0))
            .Should().BeFalse();
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.Paid));
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

        firstContext.Transactions.Add(first.AddRefund(now, PaymentMethodNames.Manual, $"r-{Guid.NewGuid():N}", 60m, 0m, null));
        secondContext.Transactions.Add(second.AddRefund(now.AddMilliseconds(1), PaymentMethodNames.Manual, $"r-{Guid.NewGuid():N}", 60m, 0m, null));
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
    /// A Gateway refund of an invoice another refund has claimed answers 409 with
    /// <c>INVOICE_REFUND_CONFLICT</c> before the card gateway is ever called.
    /// </summary>
    [Fact]
    public async Task GatewayRefundWhileAnotherIsInFlightIsAConflict()
    {
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true, RefundId = "re_never" };
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ICardPaymentGateway>(cardGateway)));
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var (invoiceId, _, _) = await TwoCardChargesAsync(host);
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var invoice = await context.Invoices.Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId);
            invoice.BeginRefund(DateTimeOffset.UtcNow);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 10m,
            refundType = nameof(RefundType.Gateway),
        });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.INVOICE_REFUND_CONFLICT);
        cardGateway.RefundInstructions.Should().BeEmpty();
    }

    /// <summary>
    /// A card gateway that throws on the second charge of a multi-charge refund answers 502 with
    /// <c>CARD_REFUND_REFUSED</c>, and the part the first charge returned is still recorded.
    /// </summary>
    [Fact]
    public async Task CardGatewayThatThrowsMidwayKeepsWhatItReturned()
    {
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true, RefundId = "re_midway", ThrowsOnRefundCall = 2 };
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ICardPaymentGateway>(cardGateway)));
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var (invoiceId, _, newerCharge) = await TwoCardChargesAsync(host);

        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 90m,
            refundType = nameof(RefundType.Gateway),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.CARD_REFUND_REFUSED);
        cardGateway.RefundInstructions.Should().HaveCount(2);
        cardGateway.RefundInstructions[0].Should().Be(new CardRefundInstruction(newerCharge, 60m, "USD", $"refund-{invoiceId}-{newerCharge}-0"));
        using var readScope = host.Services.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var refunds = await readContext.Transactions.AsNoTracking()
            .Where(t => t.InvoiceId == invoiceId && t.AmountOut > 0)
            .ToListAsync();
        refunds.Should().ContainSingle(t => t.AmountOut == 60m && t.TransactionId == "re_midway");
        (await StatusOfAsync(admin, invoiceId)).Should().Be(nameof(InvoiceStatus.PartiallyRefunded));
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
            context.Transactions.Add(invoice.AddPayment(now.AddMinutes(-5), PaymentMethodNames.Manual, $"pay-{Guid.NewGuid():N}", manualPart, 0m, null));
        }

        invoice.SetGatewaySession(HostedGateway, $"gw-order-{Guid.NewGuid():N}", now);
        context.Transactions.Add(invoice.Settle(HostedGateway, $"gw-pay-{Guid.NewGuid():N}", now)!);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }

    /// <summary>
    /// Seeds a 100 USD invoice paid by two card charges: 40 by an earlier recorded card payment and
    /// the remaining 60 by a later card settlement, each with its ledger row.
    /// </summary>
    /// <param name="host">The host whose database is seeded.</param>
    /// <returns>The invoice id, the older charge id and the newer charge id.</returns>
    private static async Task<(int InvoiceId, string OlderCharge, string NewerCharge)> TwoCardChargesAsync(
        WebApplicationFactory<Program> host)
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(host);
        var olderCharge = $"ch_{Guid.NewGuid():N}";
        var newerCharge = $"ch_{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = Invoice.Create(clientId, now.AddDays(7), "USD", now);
        invoice.AddItem("Hosting", 100m, 1);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        context.Transactions.Add(invoice.AddPayment(now.AddMinutes(-5), PaymentMethodNames.Card, olderCharge, 40m, 0m, null));
        context.Transactions.Add(invoice.Settle(PaymentMethodNames.Card, newerCharge, now)!);
        await context.SaveChangesAsync(CancellationToken.None);
        return (invoice.Id, olderCharge, newerCharge);
    }

    /// <summary>
    /// A card refund whose answer is lost after the gateway issued it records nothing and keeps the
    /// claim; a retry inside the lease is a conflict, and the retry after the lease plans the same
    /// part under the same idempotency key, so the gateway replays the first refund instead of
    /// returning the money twice, and the ledger records it once.
    /// </summary>
    [Fact]
    public async Task RefundWhoseAnswerWasLostIsReplayedByItsKeyOnceTheLeaseRunsOut()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var answerLost = false;
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true, RefundId = "re_lost" };
        cardGateway.AfterRefundIssued = _ =>
        {
            // Only the first answer is lost: the refund happened, the caller never hears of it.
            if (!answerLost)
            {
                answerLost = true;
                throw new HttpRequestException("The card gateway answer was lost.");
            }

            return Task.CompletedTask;
        };
        var (invoiceId, _, newerCharge) = await TwoCardChargesAsync(factory);
        var command = new RefundInvoicePaymentCommand(invoiceId, 50m, RefundType.Gateway);

        var lost = await RefundDirectlyAsync(command, cardGateway, clock);
        var inLease = await RefundDirectlyAsync(command, cardGateway, clock);
        clock.Advance(Invoice.RefundClaimLease + TimeSpan.FromMinutes(1));
        var afterLease = await RefundDirectlyAsync(command, cardGateway, clock);

        lost.ErrorCode.Should().Be(HostpanelErrorCodes.CARD_REFUND_REFUSED);
        inLease.ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_REFUND_CONFLICT);
        afterLease.IsSuccess.Should().BeTrue();
        var key = $"refund-{invoiceId}-{newerCharge}-0";
        cardGateway.RefundInstructions.Should().Equal(
            new CardRefundInstruction(newerCharge, 50m, "USD", key),
            new CardRefundInstruction(newerCharge, 50m, "USD", key));
        cardGateway.RefundsByKey.Should().ContainSingle();
        using var readScope = factory.Services.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var refunds = await readContext.Transactions.AsNoTracking()
            .Where(t => t.InvoiceId == invoiceId && t.AmountOut > 0)
            .ToListAsync();
        refunds.Should().ContainSingle(t => t.AmountOut == 50m && t.TransactionId == "re_lost");
    }

    /// <summary>
    /// When another writer changes the invoice while the card gateway refunds it, the save that
    /// records the refunds loses the row's concurrency check; it is retried on a fresh read, so every
    /// part the gateway issued is recorded and the other writer's change is kept.
    /// </summary>
    [Fact]
    public async Task RecordingThatLosesToAnotherWriterIsRetried()
    {
        var touched = false;
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true, RefundId = "re_retry" };
        var (invoiceId, _, _) = await TwoCardChargesAsync(factory);
        cardGateway.AfterRefundIssued = async _ =>
        {
            // Once, between the claim save and the recording save, an unrelated edit of the invoice.
            if (touched)
            {
                return;
            }

            touched = true;
            using var writerScope = factory.Services.CreateScope();
            var writerContext = writerScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var invoice = await writerContext.Invoices.SingleAsync(i => i.Id == invoiceId);
            invoice.UpdateNotes("edited meanwhile");
            await writerContext.SaveChangesAsync(CancellationToken.None);
        };

        var result = await RefundDirectlyAsync(
            new RefundInvoicePaymentCommand(invoiceId, 90m, RefundType.Gateway), cardGateway, TimeProvider.System);

        result.IsSuccess.Should().BeTrue();
        cardGateway.RefundsByKey.Should().HaveCount(2);
        using var readScope = factory.Services.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var saved = await readContext.Invoices.AsNoTracking().Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId);
        saved.AmountRefunded.Should().Be(90m);
        saved.Notes.Should().Be("edited meanwhile");
        var ledger = await readContext.Transactions.AsNoTracking()
            .Where(t => t.InvoiceId == invoiceId && t.AmountOut > 0)
            .Select(t => t.AmountOut)
            .ToListAsync();
        ledger.Should().BeEquivalentTo(new[] { 60m, 30m });
    }

    /// <summary>
    /// Runs one refund through the handler in its own scope, against the given card gateway and clock,
    /// as one request would.
    /// </summary>
    /// <param name="command">The refund to run.</param>
    /// <param name="cardGateway">The fake card gateway the refund goes to.</param>
    /// <param name="time">The clock the handler reads.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result> RefundDirectlyAsync(
        RefundInvoicePaymentCommand command, FakeCardPaymentGateway cardGateway, TimeProvider time)
    {
        using var scope = factory.Services.CreateScope();
        var handler = new RefundInvoicePaymentHandler(
            scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>(),
            scope.ServiceProvider.GetRequiredService<IPaymentPluginResolver>(),
            cardGateway,
            scope.ServiceProvider.GetRequiredService<IClientCredit>(),
            new SystemClock(time),
            NullLogger<RefundInvoicePaymentHandler>.Instance);
        return await handler.HandleAsync(command, CancellationToken.None);
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
