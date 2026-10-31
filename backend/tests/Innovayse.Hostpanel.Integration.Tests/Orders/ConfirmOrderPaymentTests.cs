using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Billing.Commands.SettleInvoiceCardPayment;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <c>POST /api/orders/{id}/confirm-payment</c> against Postgres: the shared payment-token guard,
/// the invoiced-order refusal, the card payment gateway-verification refusal, the refusal of an intent
/// that does not pay this invoice or was already recorded, and the fulfilment happy path.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ConfirmOrderPaymentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Builds a host whose card gateway is <paramref name="cardGateway"/>.</summary>
    /// <param name="cardGateway">The fake card payment gateway service to serve every call through.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(FakeCardPaymentGateway cardGateway) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ICardPaymentGateway>(cardGateway)));

    /// <summary>A settled intent opened for exactly the seeded order's invoice (25.00 USD).</summary>
    /// <param name="orderId">The order the intent names.</param>
    /// <param name="invoiceId">The invoice the intent names.</param>
    /// <param name="transactionId">The charge id the gateway reports.</param>
    /// <returns>The intent status.</returns>
    private static CardPaymentIntentStatus MatchingIntent(int orderId, int invoiceId, string transactionId) =>
        new(true, transactionId, 2500, "usd", orderId, invoiceId);

    /// <summary>A wrong payment token is refused as <c>ORDER_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task WrongTokenRefusesNotFound()
    {
        var host = BuildHost(new FakeCardPaymentGateway { IsConfigured = true });
        var (orderId, _, _) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        var client = host.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/orders/{orderId}/confirm-payment", new { paymentIntentId = "pi_x", paymentToken = "wrong" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_NOT_FOUND);
    }

    /// <summary>An order with no linked invoice is refused as <c>ORDER_NOT_INVOICED</c>.</summary>
    [Fact]
    public async Task UninvoicedOrderRefusesNotInvoiced()
    {
        var host = BuildHost(new FakeCardPaymentGateway { IsConfigured = true });
        var (orderId, paymentToken) = await OrdersTestSeeds.SeedUninvoicedOrderAsync(factory);
        var client = host.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/orders/{orderId}/confirm-payment", new { paymentIntentId = "pi_x", paymentToken });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_NOT_INVOICED);
    }

    /// <summary>A PaymentIntent card payment gateway does not confirm is refused with 502 and <c>GATEWAY_CHARGE_UPSTREAM_FAILED</c>.</summary>
    [Fact]
    public async Task UnverifiedPaymentIntentRefusesUpstreamFailed()
    {
        var host = BuildHost(new FakeCardPaymentGateway { IsConfigured = true, Verification = new(false, null, 0, "usd", null, null) });
        var (orderId, paymentToken, _) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        var client = host.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/orders/{orderId}/confirm-payment", new { paymentIntentId = "pi_x", paymentToken });

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.GATEWAY_CHARGE_UPSTREAM_FAILED);
    }

    /// <summary>A card gateway that cannot be reached is refused with 503 and <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c>, never a 500.</summary>
    [Fact]
    public async Task UnreachableGatewayRefusesServiceUnavailable()
    {
        var host = BuildHost(new FakeCardPaymentGateway
        {
            IsConfigured = true,
            Verification = new(false, null, 0, string.Empty, null, null, CardGatewayFailure.Unavailable),
        });
        var (orderId, paymentToken, _) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        var client = host.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/orders/{orderId}/confirm-payment", new { paymentIntentId = "pi_x", paymentToken });

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE);
    }

    /// <summary>
    /// A deployment with no card gateway key refuses with 409 and <c>PAYMENT_METHOD_UNAVAILABLE</c>, never
    /// a 500; the gateway is never asked to verify an intent and the order stays unfulfilled.
    /// </summary>
    [Fact]
    public async Task CardGatewayNotConfigured_RefusesPaymentMethodUnavailableAndVerifiesNothing()
    {
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = false };
        var host = BuildHost(cardGateway);
        var (orderId, paymentToken, invoiceId) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        cardGateway.Verification = MatchingIntent(orderId, invoiceId, $"txn-{Guid.NewGuid():N}");

        var response = await host.CreateClient().PostAsJsonAsync(
            $"/api/orders/{orderId}/confirm-payment", new { paymentIntentId = "pi_x", paymentToken });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PAYMENT_METHOD_UNAVAILABLE);
        cardGateway.VerifyCalls.Should().Be(0);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId)).Status.Should().NotBe(OrderStatus.Active);
    }

    /// <summary>A confirmed payment marks the invoice paid and fulfils the order, moving it to Active.</summary>
    [Fact]
    public async Task ConfirmedPaymentFulfilsTheOrder()
    {
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true };
        var host = BuildHost(cardGateway);
        var (orderId, paymentToken, invoiceId) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        cardGateway.Verification = MatchingIntent(orderId, invoiceId, $"txn-{Guid.NewGuid():N}");
        var client = host.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/orders/{orderId}/confirm-payment", new { paymentIntentId = "pi_x", paymentToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var reloaded = await context.Orders.FindAsync(orderId);
        reloaded!.Status.Should().Be(OrderStatus.Active);
    }

    /// <summary>
    /// A confirmed payment writes exactly one ledger transaction for the invoice, with its total,
    /// currency, the card payment gateway reference and the card payment gateway payment method.
    /// </summary>
    [Fact]
    public async Task ConfirmedPaymentWritesExactlyOneLedgerTransaction()
    {
        var transactionId = $"txn-{Guid.NewGuid():N}";
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true };
        var host = BuildHost(cardGateway);
        var (orderId, paymentToken, invoiceId) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        cardGateway.Verification = MatchingIntent(orderId, invoiceId, transactionId);
        var client = host.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/orders/{orderId}/confirm-payment", new { paymentIntentId = "pi_x", paymentToken });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await context.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId);
        var rows = await context.Transactions.AsNoTracking().Where(t => t.InvoiceId == invoiceId).ToListAsync();

        rows.Should().ContainSingle();
        rows[0].AmountIn.Should().Be(invoice.Total);
        rows[0].Currency.Should().Be("USD");
        rows[0].TransactionId.Should().Be(transactionId);
        rows[0].PaymentMethod.Should().Be(PaymentMethodNames.Card);
    }

    /// <summary>
    /// A settled intent that does not pay this order's invoice (another amount, currency, order or
    /// invoice) is refused as <c>PAYMENT_INTENT_MISMATCH</c> and leaves the invoice unpaid.
    /// </summary>
    /// <param name="amountMinor">The amount the intent reports, in cents (the invoice is 2500).</param>
    /// <param name="currency">The currency the intent reports (the invoice is USD).</param>
    /// <param name="orderOffset">Added to the order id the intent names; 0 names the right order.</param>
    /// <param name="invoiceOffset">Added to the invoice id the intent names; 0 names the right invoice.</param>
    [Theory]
    [InlineData(1L, "usd", 0, 0)]
    [InlineData(2500L, "eur", 0, 0)]
    [InlineData(2500L, "usd", 1, 0)]
    [InlineData(2500L, "usd", 0, 1)]
    public async Task IntentForSomethingElseRefusesMismatch(long amountMinor, string currency, int orderOffset, int invoiceOffset)
    {
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true };
        var host = BuildHost(cardGateway);
        var (orderId, paymentToken, invoiceId) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        cardGateway.Verification = new CardPaymentIntentStatus(
            true, $"txn-{Guid.NewGuid():N}", amountMinor, currency, orderId + orderOffset, invoiceId + invoiceOffset);
        var client = host.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/orders/{orderId}/confirm-payment", new { paymentIntentId = "pi_x", paymentToken });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PAYMENT_INTENT_MISMATCH);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId)).Status.Should().Be(InvoiceStatus.Unpaid);
    }

    /// <summary>An intent whose payment is already on the ledger is refused as <c>PAYMENT_INTENT_ALREADY_USED</c>.</summary>
    [Fact]
    public async Task IntentAlreadyRecordedRefusesAlreadyUsed()
    {
        var transactionId = $"txn-{Guid.NewGuid():N}";
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true };
        var host = BuildHost(cardGateway);
        var (firstOrderId, firstToken, firstInvoiceId) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        cardGateway.Verification = MatchingIntent(firstOrderId, firstInvoiceId, transactionId);
        var client = host.CreateClient();
        var first = await client.PostAsJsonAsync(
            $"/api/orders/{firstOrderId}/confirm-payment", new { paymentIntentId = "pi_x", paymentToken = firstToken });
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var (orderId, paymentToken, invoiceId) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        cardGateway.Verification = MatchingIntent(orderId, invoiceId, transactionId);
        var response = await client.PostAsJsonAsync(
            $"/api/orders/{orderId}/confirm-payment", new { paymentIntentId = "pi_x", paymentToken });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PAYMENT_INTENT_ALREADY_USED);
    }

    /// <summary>
    /// A retry after the invoice was settled by this same intent but before the order was fulfilled
    /// (a crash between the two) answers success and fulfils the order, instead of refusing the
    /// intent as already used and leaving the paid order pending for ever.
    /// </summary>
    [Fact]
    public async Task RetryAfterSettlementFulfilsTheOrder()
    {
        var transactionId = $"txn-{Guid.NewGuid():N}";
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true };
        var host = BuildHost(cardGateway);
        var (orderId, paymentToken, invoiceId) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        cardGateway.Verification = MatchingIntent(orderId, invoiceId, transactionId);
        using (var scope = host.Services.CreateScope())
        {
            // The first attempt got as far as the settlement and no further.
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            (await bus.InvokeAsync<Result>(
                new SettleInvoiceCardPaymentCommand(invoiceId, transactionId, 25m, "USD"))).IsSuccess.Should().BeTrue();
        }

        var response = await host.CreateClient().PostAsJsonAsync(
            $"/api/orders/{orderId}/confirm-payment", new { paymentIntentId = "pi_x", paymentToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var check = host.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId)).Status.Should().Be(OrderStatus.Active);
        (await context.Transactions.AsNoTracking().CountAsync(t => t.TransactionId == transactionId)).Should().Be(1);
        cardGateway.RefundInstructions.Should().BeEmpty();
    }

    /// <summary>
    /// A settled intent for this order's invoice but for another amount took money the invoice
    /// cannot record, so it is refunded rather than kept, and the invoice stays unpaid.
    /// </summary>
    [Fact]
    public async Task IntentForAnotherAmountIsRefunded()
    {
        var transactionId = $"txn-{Guid.NewGuid():N}";
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true };
        var host = BuildHost(cardGateway);
        var (orderId, paymentToken, invoiceId) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        cardGateway.Verification = new CardPaymentIntentStatus(true, transactionId, 3000, "usd", orderId, invoiceId);

        var response = await host.CreateClient().PostAsJsonAsync(
            $"/api/orders/{orderId}/confirm-payment", new { paymentIntentId = "pi_x", paymentToken });

        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PAYMENT_INTENT_MISMATCH);
        cardGateway.RefundInstructions.Should().ContainSingle()
            .Which.Should().Be(new CardRefundInstruction(transactionId, 30m, "usd", $"return-{transactionId}"));
    }
}
