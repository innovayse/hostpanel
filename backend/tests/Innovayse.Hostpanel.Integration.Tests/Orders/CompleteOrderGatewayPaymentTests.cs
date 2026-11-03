using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.SDK.Plugins.Enums;
using Innovayse.SDK.Plugins.Payment;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <c>POST /api/orders/{id}/gateway-payment/complete</c> against Postgres: the shared
/// payment-token guard, the invoiced-order refusal, and the fulfilment happy path.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CompleteOrderGatewayPaymentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Builds a host whose payment plugins come from <paramref name="resolver"/>.</summary>
    /// <param name="resolver">The fake resolver the host should resolve plugins through.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(FakePaymentPluginResolver resolver) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentPluginResolver>(resolver)));

    /// <summary>A wrong payment token is refused as <c>ORDER_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task WrongTokenRefusesNotFound()
    {
        var host = BuildHost(new FakePaymentPluginResolver());
        var (orderId, _, _) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        var client = host.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/orders/{orderId}/gateway-payment/complete", new { paymentToken = "wrong" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_NOT_FOUND);
    }

    /// <summary>An order with no linked invoice is refused as <c>ORDER_NOT_INVOICED</c>.</summary>
    [Fact]
    public async Task UninvoicedOrderRefusesNotInvoiced()
    {
        var host = BuildHost(new FakePaymentPluginResolver());
        var (orderId, paymentToken) = await OrdersTestSeeds.SeedUninvoicedOrderAsync(factory);
        var client = host.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/orders/{orderId}/gateway-payment/complete", new { paymentToken });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_NOT_INVOICED);
    }

    /// <summary>
    /// Completing a gateway payment the bank reports as paid marks the invoice paid and fulfils
    /// the order: the order moves from Pending to Active.
    /// </summary>
    [Fact]
    public async Task CompleteWhenTheGatewayReportsPaidFulfilsTheOrder()
    {
        var plugin = new FakePaymentPlugin
        {
            CurrencyCode = "840",
            StatusResult = new GatewayPaymentStatus(GatewayPaymentState.Paid, "gw-txn-1", "orderStatus:2", AmountMinor: 2500),
        };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        var (orderId, paymentToken, _) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);

        // The invoice needs a live gateway session before "complete" has anything to verify:
        // the same session "start" would have written.
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var order = await context.Orders.FindAsync(orderId);
            var invoice = await context.Invoices.FindAsync(order!.InvoiceId!.Value);
            invoice!.SetGatewaySession("bank-gateway", "gw-order-1", scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var client = host.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/orders/{orderId}/gateway-payment/complete", new { paymentToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("state").GetString().Should().Be(nameof(GatewayCompletionState.Paid));

        using var readScope = host.Services.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var reloaded = await readContext.Orders.FindAsync(orderId);
        reloaded!.Status.Should().Be(OrderStatus.Active);

        // The return page shows the stored number rather than building one from the id.
        body.GetProperty("orderNumber").GetString().Should().Be(reloaded.OrderNumber);
    }

    /// <summary>
    /// A completion whose save landed but whose fulfilment failed is finished by the retry: the
    /// invoice is already settled, and the retry still fulfils the order rather than answering Paid
    /// with the order left pending.
    /// </summary>
    [Fact]
    public async Task RetryOfASettledInvoiceFulfilsTheOrder()
    {
        var plugin = new FakePaymentPlugin
        {
            CurrencyCode = "840",
            StatusResult = new GatewayPaymentStatus(GatewayPaymentState.Paid, "gw-txn-2", "orderStatus:2", AmountMinor: 2500),
        };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        var (orderId, paymentToken, _) = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);

        // The first completion's save: the invoice settled through its session and the ledger row
        // written, with no fulfilment after it.
        using (var scope = host.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var now = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow;
            var order = await context.Orders.FindAsync(orderId);
            var invoice = await context.Invoices.Include(i => i.Transactions).SingleAsync(i => i.Id == order!.InvoiceId!.Value);
            invoice.SetGatewaySession("bank-gateway", "gw-order-2", now);
            context.Transactions.Add(invoice.Settle("bank-gateway", "gw-txn-2", now, TransactionSource.Manual)!);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var client = host.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/orders/{orderId}/gateway-payment/complete", new { paymentToken });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var readScope = host.Services.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await readContext.Orders.FindAsync(orderId))!.Status.Should().Be(OrderStatus.Active);
    }
}
