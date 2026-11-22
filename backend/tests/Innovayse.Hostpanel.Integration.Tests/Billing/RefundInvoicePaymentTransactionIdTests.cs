using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// What a hosted gateway refund tells the plugin about the payment it refunds: <c>RefundInstruction.TransactionId</c> is the
/// transaction id the plugin itself reported when the payment became paid, read from the client ledger, and
/// <see langword="null"/> when the host holds only the order id (the plugin reported none). The refund route itself is
/// <see cref="RefundInvoicePaymentTests"/>'s.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RefundInvoicePaymentTransactionIdTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The hosted gateway module the invoices in these tests were paid through.</summary>
    private const string HostedGateway = "bank-gateway";

    /// <summary>The instruction carries the transaction id the plugin reported for the payment.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GatewayRefund_CarriesTheTransactionIdThePluginReported()
    {
        var reference = $"tx-{Guid.NewGuid():N}";
        var (host, plugin) = BuildHost();
        var invoiceId = await PaidThroughGatewayAsync(host, $"order-{Guid.NewGuid():N}", reference);

        await RefundAsync(host, invoiceId);

        plugin.RefundInstructions.Should().ContainSingle().Which.TransactionId.Should().Be(reference);
    }

    /// <summary>A payment the plugin gave no transaction id for is recorded under the order id, and the plugin is told the host holds none.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GatewayRefund_WhenTheLedgerHoldsTheOrderId_CarriesNoTransactionId()
    {
        var orderId = $"order-{Guid.NewGuid():N}";
        var (host, plugin) = BuildHost();
        var invoiceId = await PaidThroughGatewayAsync(host, orderId, orderId);

        await RefundAsync(host, invoiceId);

        plugin.RefundInstructions.Should().ContainSingle().Which.TransactionId.Should().BeNull();
    }

    /// <summary>With more than one payment row of the gateway on the invoice, the newest one names the transaction.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GatewayRefund_UsesTheNewestPaymentRowOfTheGateway()
    {
        var older = $"tx-old-{Guid.NewGuid():N}";
        var newer = $"tx-new-{Guid.NewGuid():N}";
        var (host, plugin) = BuildHost();
        var invoiceId = await PaidThroughGatewayAsync(host, $"order-{Guid.NewGuid():N}", older);
        await AddLedgerRowAsync(host, invoiceId, newer, TestMoments.Seeded.AddHours(1), linkedToInvoice: true);

        await RefundAsync(host, invoiceId);

        plugin.RefundInstructions.Should().ContainSingle().Which.TransactionId.Should().Be(newer);
    }

    /// <summary>Money the gateway took that no invoice recorded (a hand back to the balance, with no invoice link) is not this payment's id.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GatewayRefund_IgnoresALedgerRowWithNoInvoiceLink()
    {
        var reference = $"tx-{Guid.NewGuid():N}";
        var (host, plugin) = BuildHost();
        var invoiceId = await PaidThroughGatewayAsync(host, $"order-{Guid.NewGuid():N}", reference);
        await AddLedgerRowAsync(host, invoiceId, $"tx-held-{Guid.NewGuid():N}", TestMoments.Seeded.AddHours(1), linkedToInvoice: false);

        await RefundAsync(host, invoiceId);

        plugin.RefundInstructions.Should().ContainSingle().Which.TransactionId.Should().Be(reference);
    }

    /// <summary>Builds a host whose bank gateway is a fake that accepts one refund.</summary>
    /// <returns>The host and the plugin it resolves to.</returns>
    private (WebApplicationFactory<Program> Host, FakePaymentPlugin Plugin) BuildHost()
    {
        var plugin = new FakePaymentPlugin { RefundResult = $"gw-refund-{Guid.NewGuid():N}" };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(HostedGateway, plugin);
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IPaymentPluginResolver>(resolver)));
        return (host, plugin);
    }

    /// <summary>Asks for a gateway refund of 10 on an invoice, as an administrator would.</summary>
    /// <param name="host">The host to ask.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>A task that completes once the refund was answered.</returns>
    private async Task RefundAsync(WebApplicationFactory<Program> host, int invoiceId)
    {
        var admin = await AuthTestSeeds.AdminClientAsync(factory, host);
        var response = await admin.PostAsJsonAsync($"/api/billing/{invoiceId}/refund", new
        {
            amount = 10m,
            refundType = nameof(RefundType.Gateway),
        });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Seeds a 100 USD invoice settled in full through the hosted gateway, with the payment on the ledger as a gateway payment.</summary>
    /// <param name="host">The host whose database is seeded.</param>
    /// <param name="orderId">The gateway order id of the session.</param>
    /// <param name="reference">The reference the payment is recorded under.</param>
    /// <returns>The invoice id.</returns>
    private static async Task<int> PaidThroughGatewayAsync(WebApplicationFactory<Program> host, string orderId, string reference)
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(host);
        var now = TestMoments.Seeded;

        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = Invoice.Create(clientId, now.AddDays(7), "USD", now);
        invoice.AddItem("Hosting", 100m, 1);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);

        invoice.SetGatewaySession(HostedGateway, orderId, now);
        context.Transactions.Add(invoice.Settle(HostedGateway, reference, now, TransactionSource.GatewayPayment)!);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }

    /// <summary>Adds one more gateway payment row to the client ledger of an invoice's client.</summary>
    /// <param name="host">The host whose database is written.</param>
    /// <param name="invoiceId">The invoice whose client the row belongs to.</param>
    /// <param name="reference">The reference the row is recorded under.</param>
    /// <param name="date">When the row was written.</param>
    /// <param name="linkedToInvoice">Whether the row names the invoice; a hand back to the balance does not.</param>
    /// <returns>A task that completes once saved.</returns>
    private static async Task AddLedgerRowAsync(
        WebApplicationFactory<Program> host, int invoiceId, string reference, DateTimeOffset date, bool linkedToInvoice)
    {
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var clientId = (await context.Invoices.FindAsync([invoiceId], CancellationToken.None))!.ClientId;
        context.Transactions.Add(Transaction.Create(
            clientId, date, "Gateway payment", reference, linkedToInvoice ? invoiceId : null, HostedGateway, 10m, 0m, 0m, false, "USD",
            TransactionSource.GatewayPayment));
        await context.SaveChangesAsync(CancellationToken.None);
    }
}
