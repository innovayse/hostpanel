using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Innovayse.Hostpanel.Domain.Billing.Enums;
namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <c>POST /api/orders/{id}/create-payment-intent</c> against Postgres: the shared payment-token
/// guard, the invoiced-order refusal, and the happy path with the card gateway faked.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateOrderPaymentIntentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Builds a host whose card gateway is <see cref="FakeCardPaymentGateway"/>.</summary>
    /// <param name="cardGateway">The fake card gateway to serve every call through.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(FakeCardPaymentGateway cardGateway) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ICardPaymentGateway>(cardGateway)));

    /// <summary>A wrong payment token is refused as <c>ORDER_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task WrongTokenRefusesNotFound()
    {
        var (orderId, _, _) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/orders/{orderId}/create-payment-intent", new { paymentToken = "wrong" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_NOT_FOUND);
    }

    /// <summary>An order with no linked invoice is refused as <c>ORDER_NOT_INVOICED</c>.</summary>
    [Fact]
    public async Task UninvoicedOrderRefusesNotInvoiced()
    {
        var (orderId, paymentToken) = await OrdersTestSeeds.SeedUninvoicedOrderAsync(factory);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/orders/{orderId}/create-payment-intent", new { paymentToken });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_NOT_INVOICED);
    }

    /// <summary>
    /// An invoice that owes nothing more is refused with <c>INVOICE_NOT_PAYABLE</c> and no intent is
    /// opened, since the gateway refuses a payment for zero.
    /// </summary>
    [Fact]
    public async Task NothingDueRefusesNotPayable()
    {
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true };
        var host = BuildHost(cardGateway);
        var (orderId, paymentToken, invoiceId) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var invoice = await context.Invoices.Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId);
            context.Transactions.Add(invoice.AddPayment(
                DateTimeOffset.UtcNow, PaymentMethodNames.Manual, $"full-{Guid.NewGuid():N}", 25m, 0m, null, TransactionSource.Manual));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var response = await host.CreateClient().PostAsJsonAsync(
            $"/api/orders/{orderId}/create-payment-intent", new { paymentToken });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        cardGateway.LastIntentInstruction.Should().BeNull();
    }

    /// <summary>The right token on an invoiced order opens a PaymentIntent and returns its client secret.</summary>
    [Fact]
    public async Task RightTokenReturnsAClientSecret()
    {
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true, IntentClientSecret = "pi_test_secret_42" };
        var host = BuildHost(cardGateway);
        var (orderId, paymentToken, invoiceId) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        var client = host.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/orders/{orderId}/create-payment-intent", new { paymentToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("clientSecret").GetString().Should().Be("pi_test_secret_42");

        // The ids travel as typed fields, so the gateway can tie the payment back to its rows.
        cardGateway.LastIntentInstruction.Should().NotBeNull();
        cardGateway.LastIntentInstruction!.OrderId.Should().Be(orderId);
        cardGateway.LastIntentInstruction.InvoiceId.Should().Be(invoiceId);

        // The intent is opened in the invoice's own currency for its total.
        cardGateway.LastIntentInstruction.Currency.Should().Be("USD");
        cardGateway.LastIntentInstruction.Amount.Should().Be(25m);
    }

    /// <summary>An order billed in another currency opens its intent in that currency, never a fixed one.</summary>
    [Fact]
    public async Task IntentUsesTheInvoiceCurrency()
    {
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true };
        var host = BuildHost(cardGateway);
        var (orderId, paymentToken, _) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory, "EUR");
        var client = host.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/orders/{orderId}/create-payment-intent", new { paymentToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        cardGateway.LastIntentInstruction!.Currency.Should().Be("EUR");
    }

    /// <summary>
    /// On a part-paid invoice the intent is opened for what is still due, the amount the settlement
    /// records, never the total: the payer is not charged twice for what was already paid.
    /// </summary>
    [Fact]
    public async Task IntentIsOpenedForTheAmountStillDue()
    {
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true };
        var host = BuildHost(cardGateway);
        var (orderId, paymentToken, invoiceId) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var invoice = await context.Invoices.Include(i => i.Transactions).SingleAsync(i => i.Id == invoiceId);
            context.Transactions.Add(invoice.AddPayment(
                DateTimeOffset.UtcNow, PaymentMethodNames.Manual, $"part-{Guid.NewGuid():N}", 10m, 0m, null, TransactionSource.Manual));
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var response = await host.CreateClient().PostAsJsonAsync(
            $"/api/orders/{orderId}/create-payment-intent", new { paymentToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        cardGateway.LastIntentInstruction!.Amount.Should().Be(15m);
    }

    /// <summary>
    /// A card gateway that cannot be reached answers 503 with <c>PAYMENT_GATEWAY_SERVICE_UNAVAILABLE</c>,
    /// and one that refuses the intent answers 502 with <c>GATEWAY_CHARGE_UPSTREAM_FAILED</c>, never a 500.
    /// </summary>
    /// <param name="failure">The failure the fake gateway reports.</param>
    /// <param name="status">The status the response must carry.</param>
    /// <param name="code">The error code the response must carry.</param>
    [Theory]
    [InlineData(CardGatewayFailure.Unavailable, HttpStatusCode.ServiceUnavailable, HostpanelErrorCodes.PAYMENT_GATEWAY_SERVICE_UNAVAILABLE)]
    [InlineData(CardGatewayFailure.Refused, HttpStatusCode.BadGateway, HostpanelErrorCodes.GATEWAY_CHARGE_UPSTREAM_FAILED)]
    public async Task GatewayFailureAnswersWithItsCode(CardGatewayFailure failure, HttpStatusCode status, string code)
    {
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true, IntentFailure = failure };
        var host = BuildHost(cardGateway);
        var (orderId, paymentToken, _) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);

        var response = await host.CreateClient().PostAsJsonAsync(
            $"/api/orders/{orderId}/create-payment-intent", new { paymentToken });

        response.StatusCode.Should().Be(status);
        await OrdersTestSeeds.AssertCodeAsync(response, code);
    }

    /// <summary>
    /// A retry for the same order and the same amount due is opened under the same idempotency key, so
    /// the card gateway answers with the intent it already opened instead of a second one that could
    /// be paid as well.
    /// </summary>
    [Fact]
    public async Task RepeatedRequestsUseTheSameIdempotencyKey()
    {
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true };
        var host = BuildHost(cardGateway);
        var (orderId, paymentToken, _) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        var client = host.CreateClient();

        await client.PostAsJsonAsync($"/api/orders/{orderId}/create-payment-intent", new { paymentToken });
        var firstKey = cardGateway.LastIntentInstruction!.IdempotencyKey;
        await client.PostAsJsonAsync($"/api/orders/{orderId}/create-payment-intent", new { paymentToken });

        cardGateway.LastIntentInstruction!.IdempotencyKey.Should().Be(firstKey);
    }

    /// <summary>
    /// A deployment with no card gateway key refuses with 409 and <c>PAYMENT_METHOD_UNAVAILABLE</c>, never
    /// a 500, and the gateway is never asked for an intent.
    /// </summary>
    [Fact]
    public async Task CardGatewayNotConfigured_RefusesPaymentMethodUnavailableAndOpensNoIntent()
    {
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = false };
        var host = BuildHost(cardGateway);
        var (orderId, paymentToken, _) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);

        var response = await host.CreateClient().PostAsJsonAsync(
            $"/api/orders/{orderId}/create-payment-intent", new { paymentToken });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.PAYMENT_METHOD_UNAVAILABLE);
        cardGateway.LastIntentInstruction.Should().BeNull();
    }

    /// <summary>
    /// With no card gateway key a wrong payment token is still <c>ORDER_NOT_FOUND</c>: the order checks
    /// run before the gateway check, so the missing key does not reveal anything about an order.
    /// </summary>
    [Fact]
    public async Task CardGatewayNotConfigured_WrongTokenStillRefusesNotFound()
    {
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = false };
        var host = BuildHost(cardGateway);
        var (orderId, _, _) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);

        var response = await host.CreateClient().PostAsJsonAsync(
            $"/api/orders/{orderId}/create-payment-intent", new { paymentToken = "wrong" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_NOT_FOUND);
        cardGateway.LastIntentInstruction.Should().BeNull();
    }

    /// <summary>An order of an account with card processing disabled opens no intent: 403 and the gateway is never asked.</summary>
    [Fact]
    public async Task AccountWithCardProcessingDisabledIsRefused()
    {
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true, IntentClientSecret = "pi_test_secret_disabled" };
        var host = BuildHost(cardGateway);
        var (orderId, paymentToken, _) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var clientId = await context.Orders.AsNoTracking().Where(o => o.Id == orderId).Select(o => o.ClientId).FirstAsync();
            await ClientSettingsSeeds.SetAsync(factory, clientId, disableCcProcessing: true);
        }

        var response = await host.CreateClient().PostAsJsonAsync(
            $"/api/orders/{orderId}/create-payment-intent", new { paymentToken });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.CLIENT_CARD_PAYMENT_DISABLED);
        cardGateway.LastIntentInstruction.Should().BeNull();
    }
}
