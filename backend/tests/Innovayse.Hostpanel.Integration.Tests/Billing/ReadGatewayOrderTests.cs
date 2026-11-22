using Hostpanel.Sdk.Plugins.Enums;
using Innovayse.Hostpanel.Application.Billing.Commands.ReadGatewayOrder;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// "Read a gateway order" (<see cref="ReadGatewayOrderCommand"/>), the use case behind a payment plugin's
/// <c>RequestOrderReadAsync</c>, against Postgres with the payment plugin replaced by <see cref="FakePaymentPluginResolver"/>.
/// What it settles is settled by the one completion path (<c>CompleteGatewayPaymentCommand</c>), so these tests pin that a hint
/// moves nothing by itself: the plugin's status read decides, a repeat settles once, and an order that is not the plugin's
/// is left alone without a call to the gateway. A payment reported as reversed after the settlement changes nothing and tells
/// staff once.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ReadGatewayOrderTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The plugin id every seeded session belongs to.</summary>
    private const string Gateway = "bank-gateway";

    /// <summary>Builds a host whose payment plugins come from <paramref name="resolver"/>.</summary>
    /// <param name="resolver">The fake resolver the host should resolve plugins through.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(FakePaymentPluginResolver resolver) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IPaymentPluginResolver>(resolver)));

    /// <summary>A plugin that reports the full 10.00 of its order paid, under a reference of its own.</summary>
    /// <param name="reference">The gateway's transaction id of the payment.</param>
    /// <returns>The plugin.</returns>
    private static FakePaymentPlugin PaidPlugin(string reference) =>
        new() { StatusResult = new(GatewayPaymentState.Paid, reference, "orderStatus:2", AmountMinor: 1000) };

    /// <summary>A resolver that hands back one plugin for the bank gateway.</summary>
    /// <param name="plugin">The plugin to register.</param>
    /// <returns>The resolver.</returns>
    private static FakePaymentPluginResolver ResolverOf(FakePaymentPlugin plugin)
    {
        var resolver = new FakePaymentPluginResolver();
        resolver.Register(Gateway, plugin);
        return resolver;
    }

    /// <summary>A paid order settles the invoice through the one completion path: the ledger row, the paid status and the payment event.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PaidOrder_SettlesTheInvoiceThroughTheCompletionPath()
    {
        var reference = $"ref-{Guid.NewGuid():N}";
        var plugin = PaidPlugin(reference);
        var host = BuildHost(ResolverOf(plugin));
        var (invoiceId, orderId) = await BillingTestSeeds.SeedInvoiceWithSessionAsync(host, Gateway);

        var read = await ReadAsync(host, Gateway, orderId);

        read.Result.IsSuccess.Should().BeTrue();
        read.Received.Should().ContainSingle(e => e.InvoiceId == invoiceId);
        (await InvoiceAsync(invoiceId)).Status.Should().Be(InvoiceStatus.Paid);
        var ledger = await LedgerOfAsync(invoiceId);
        ledger.Should().ContainSingle().Which.TransactionId.Should().Be(reference);
        ledger[0].Source.Should().Be(TransactionSource.GatewayPayment);
    }

    /// <summary>The same hint twice settles once: one ledger row and one payment event, and the second read asks the gateway about nothing new.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheSameHintTwice_SettlesOnce()
    {
        var plugin = PaidPlugin($"ref-{Guid.NewGuid():N}");
        var host = BuildHost(ResolverOf(plugin));
        var (invoiceId, orderId) = await BillingTestSeeds.SeedInvoiceWithSessionAsync(host, Gateway);

        var first = await ReadAsync(host, Gateway, orderId);
        var second = await ReadAsync(host, Gateway, orderId);

        first.Received.Should().ContainSingle();
        second.Received.Should().BeEmpty();
        second.Reversed.Should().BeEmpty();
        (await LedgerOfAsync(invoiceId)).Should().ContainSingle();
        (await InvoiceAsync(invoiceId)).Status.Should().Be(InvoiceStatus.Paid);
    }

    /// <summary>A pending order changes nothing: the invoice stays open and no ledger row is written.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PendingOrder_ChangesNothing()
    {
        var plugin = new FakePaymentPlugin();
        var host = BuildHost(ResolverOf(plugin));
        var (invoiceId, orderId) = await BillingTestSeeds.SeedInvoiceWithSessionAsync(host, Gateway);

        var read = await ReadAsync(host, Gateway, orderId);

        read.Result.IsSuccess.Should().BeTrue();
        plugin.GetStatusCallCount.Should().Be(1);
        (await InvoiceAsync(invoiceId)).IsSettled.Should().BeFalse();
        (await LedgerOfAsync(invoiceId)).Should().BeEmpty();
    }

    /// <summary>An order no invoice holds is ignored without a call to the gateway, and the sender is told nothing different.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownOrder_IsIgnoredWithoutAGatewayCall()
    {
        var plugin = PaidPlugin($"ref-{Guid.NewGuid():N}");
        var host = BuildHost(ResolverOf(plugin));

        var read = await ReadAsync(host, Gateway, $"no-such-{Guid.NewGuid():N}");

        read.Result.IsSuccess.Should().BeTrue();
        plugin.GetStatusCallCount.Should().Be(0);
    }

    /// <summary>Another plugin's order is never read through a plugin that does not own it: nothing is settled and nothing is asked.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task OrderOfAnotherPlugin_IsIgnoredWithoutAGatewayCall()
    {
        var owner = PaidPlugin($"ref-{Guid.NewGuid():N}");
        var intruder = PaidPlugin($"ref-{Guid.NewGuid():N}");
        var resolver = ResolverOf(owner);
        resolver.Register("other-gateway", intruder);
        var host = BuildHost(resolver);
        var (invoiceId, orderId) = await BillingTestSeeds.SeedInvoiceWithSessionAsync(host, Gateway);

        var read = await ReadAsync(host, "other-gateway", orderId);

        read.Result.IsSuccess.Should().BeTrue();
        owner.GetStatusCallCount.Should().Be(0);
        intruder.GetStatusCallCount.Should().Be(0);
        (await InvoiceAsync(invoiceId)).IsSettled.Should().BeFalse();
    }

    /// <summary>A session a newer attempt replaced is not the invoice's any more: reading the old order id does nothing.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReplacedSession_IsIgnored()
    {
        var plugin = PaidPlugin($"ref-{Guid.NewGuid():N}");
        var host = BuildHost(ResolverOf(plugin));
        var (invoiceId, oldOrderId) = await BillingTestSeeds.SeedInvoiceWithSessionAsync(host, Gateway);
        await ReplaceSessionAsync(invoiceId, $"gw-{Guid.NewGuid():N}");

        var read = await ReadAsync(host, Gateway, oldOrderId);

        read.Result.IsSuccess.Should().BeTrue();
        plugin.GetStatusCallCount.Should().Be(0);
        (await InvoiceAsync(invoiceId)).IsSettled.Should().BeFalse();
    }

    /// <summary>A gateway that reports another amount than the invoice owes is not settled: the use case answers the mismatch for its caller's log.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task WrongAmount_IsNotSettled()
    {
        var plugin = new FakePaymentPlugin
        {
            StatusResult = new(GatewayPaymentState.Paid, $"ref-{Guid.NewGuid():N}", "orderStatus:2", AmountMinor: 500),
        };
        var host = BuildHost(ResolverOf(plugin));
        var (invoiceId, orderId) = await BillingTestSeeds.SeedInvoiceWithSessionAsync(host, Gateway);

        var read = await ReadAsync(host, Gateway, orderId);

        read.Result.ErrorCode.Should().Be(HostpanelErrorCodes.GATEWAY_PAYMENT_MISMATCH);
        (await InvoiceAsync(invoiceId)).IsSettled.Should().BeFalse();
        (await LedgerOfAsync(invoiceId)).Should().BeEmpty();
    }

    /// <summary>
    /// A payment the gateway later reports as no longer paid changes nothing on the invoice or the ledger, marks the session
    /// and tells staff once.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReversalAfterPaid_TellsStaffOnceAndChangesNoMoney()
    {
        var plugin = PaidPlugin($"ref-{Guid.NewGuid():N}");
        var host = BuildHost(ResolverOf(plugin));
        var (invoiceId, orderId) = await BillingTestSeeds.SeedInvoiceWithSessionAsync(host, Gateway);
        await ReadAsync(host, Gateway, orderId);
        plugin.StatusResult = new(GatewayPaymentState.Declined, null, "orderStatus:4");

        var reversal = await ReadAsync(host, Gateway, orderId);

        var alert = reversal.Reversed.Should().ContainSingle().Which;
        alert.InvoiceId.Should().Be(invoiceId);
        alert.Module.Should().Be(Gateway);
        var invoice = await InvoiceAsync(invoiceId);
        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.GatewayReversalNotifiedAt.Should().NotBeNull();
        (await LedgerOfAsync(invoiceId)).Should().ContainSingle();
    }

    /// <summary>A second hint for a reversal already told sends nothing and does not ask the gateway again.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReversalTwice_TellsStaffOnlyOnce()
    {
        var plugin = PaidPlugin($"ref-{Guid.NewGuid():N}");
        var host = BuildHost(ResolverOf(plugin));
        var (_, orderId) = await BillingTestSeeds.SeedInvoiceWithSessionAsync(host, Gateway);
        await ReadAsync(host, Gateway, orderId);
        plugin.StatusResult = new(GatewayPaymentState.Declined, null, "orderStatus:4");
        await ReadAsync(host, Gateway, orderId);
        var callsAfterTheFirst = plugin.GetStatusCallCount;

        var again = await ReadAsync(host, Gateway, orderId);

        again.Reversed.Should().BeEmpty();
        plugin.GetStatusCallCount.Should().Be(callsAfterTheFirst);
    }

    /// <summary>A reversal the invoice explains by a refund of its own through the gateway is no news: no alert, no mark, no call.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReversalAfterARefundThroughTheGateway_TellsNobody()
    {
        var plugin = PaidPlugin($"ref-{Guid.NewGuid():N}");
        var host = BuildHost(ResolverOf(plugin));
        var (invoiceId, orderId) = await BillingTestSeeds.SeedInvoiceWithSessionAsync(host, Gateway);
        await ReadAsync(host, Gateway, orderId);
        await RecordGatewayRefundAsync(invoiceId, 4m);
        plugin.StatusResult = new(GatewayPaymentState.Declined, null, "orderStatus:4");
        var callsBefore = plugin.GetStatusCallCount;

        var reversal = await ReadAsync(host, Gateway, orderId);

        reversal.Reversed.Should().BeEmpty();
        plugin.GetStatusCallCount.Should().Be(callsBefore);
        (await InvoiceAsync(invoiceId)).GatewayReversalNotifiedAt.Should().BeNull();
    }

    /// <summary>A reversal that arrives while the host's own refund waits for the gateway is explained by it: no alert.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReversalWhileARefundIsInFlight_TellsNobody()
    {
        var plugin = PaidPlugin($"ref-{Guid.NewGuid():N}");
        var host = BuildHost(ResolverOf(plugin));
        var (invoiceId, orderId) = await BillingTestSeeds.SeedInvoiceWithSessionAsync(host, Gateway);
        await ReadAsync(host, Gateway, orderId);
        await ClaimRefundAsync(invoiceId, 4m);
        plugin.StatusResult = new(GatewayPaymentState.Declined, null, "orderStatus:4");

        var reversal = await ReadAsync(host, Gateway, orderId);

        reversal.Reversed.Should().BeEmpty();
        (await InvoiceAsync(invoiceId)).GatewayReversalNotifiedAt.Should().BeNull();
    }

    /// <summary>A settled order the gateway still reports as paid is no news: no alert and no mark.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PaidAgainAfterSettled_TellsNobody()
    {
        var plugin = PaidPlugin($"ref-{Guid.NewGuid():N}");
        var host = BuildHost(ResolverOf(plugin));
        var (invoiceId, orderId) = await BillingTestSeeds.SeedInvoiceWithSessionAsync(host, Gateway);
        await ReadAsync(host, Gateway, orderId);

        var again = await ReadAsync(host, Gateway, orderId);

        again.Reversed.Should().BeEmpty();
        (await InvoiceAsync(invoiceId)).GatewayReversalNotifiedAt.Should().BeNull();
    }

    /// <summary>A settled order whose gateway is still pending is no news either: pending is not a reversal.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PendingAfterSettled_TellsNobody()
    {
        var plugin = PaidPlugin($"ref-{Guid.NewGuid():N}");
        var host = BuildHost(ResolverOf(plugin));
        var (invoiceId, orderId) = await BillingTestSeeds.SeedInvoiceWithSessionAsync(host, Gateway);
        await ReadAsync(host, Gateway, orderId);
        plugin.StatusResult = new(GatewayPaymentState.Pending, null, "orderStatus:0");

        var again = await ReadAsync(host, Gateway, orderId);

        again.Reversed.Should().BeEmpty();
        (await InvoiceAsync(invoiceId)).GatewayReversalNotifiedAt.Should().BeNull();
    }

    /// <summary>Reads one order through the bus of <paramref name="host"/> and waits until everything it published has been handled.</summary>
    /// <param name="host">The host whose bus runs the use case.</param>
    /// <param name="pluginId">The plugin the read is made for.</param>
    /// <param name="gatewayOrderId">The gateway order id.</param>
    /// <returns>What the use case answered, the reversal alerts and the payment events published meanwhile.</returns>
    private static async Task<(Result Result, IReadOnlyList<GatewayPaymentReversedEvent> Reversed, IReadOnlyList<PaymentReceivedEvent> Received)> ReadAsync(WebApplicationFactory<Program> host, string pluginId, string gatewayOrderId)
    {
        Result? result = null;
        var session = await host.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(async _ =>
            {
                using var scope = host.Services.CreateScope();
                result = await scope.ServiceProvider.GetRequiredService<IMessageBus>()
                    .InvokeAsync<Result>(new ReadGatewayOrderCommand(pluginId, gatewayOrderId));
            }));

        return (
            result!,
            session.Executed.MessagesOf<GatewayPaymentReversedEvent>().ToList(),
            // Several handlers listen to a payment event and each runs as its own execution (separated handlers), so the
            // session lists one event once per handler. The event is a record, so Distinct folds them back to what was published.
            session.Executed.MessagesOf<PaymentReceivedEvent>().Distinct().ToList());
    }

    /// <summary>Starts a new session on an invoice, as a second payment attempt does, replacing the old one.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="orderId">The gateway order id of the new session.</param>
    /// <returns>A task that completes once saved.</returns>
    private async Task ReplaceSessionAsync(int invoiceId, string orderId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Invoices.FirstAsync(i => i.Id == invoiceId))
            .SetGatewaySession(Gateway, orderId, scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Records a refund the gateway made on an invoice settled through it, as the refund use case does once the gateway answers.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="amount">The amount refunded.</param>
    /// <returns>A task that completes once saved.</returns>
    private async Task RecordGatewayRefundAsync(int invoiceId, decimal amount)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await context.Invoices.Include(i => i.Transactions).FirstAsync(i => i.Id == invoiceId);
        context.Transactions.Add(invoice.AddRefund(
            scope.ServiceProvider.GetRequiredService<IClock>().UtcNow, Gateway, $"refund-{Guid.NewGuid():N}", amount, 0m, null, false,
            TransactionSource.GatewayRefund));
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Claims an invoice for a gateway refund that is still waiting for its answer.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="amount">The amount being refunded.</param>
    /// <returns>A task that completes once saved.</returns>
    private async Task ClaimRefundAsync(int invoiceId, decimal amount)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await context.Invoices.Include(i => i.Transactions).FirstAsync(i => i.Id == invoiceId);
        invoice.BeginGatewayRefund(scope.ServiceProvider.GetRequiredService<IClock>().UtcNow, amount);
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Reads an invoice as the database holds it.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The invoice, untracked.</returns>
    private async Task<Invoice> InvoiceAsync(int invoiceId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().Invoices
            .AsNoTracking().SingleAsync(i => i.Id == invoiceId);
    }

    /// <summary>Reads the client ledger rows written for an invoice.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The rows, oldest first.</returns>
    private async Task<List<Transaction>> LedgerOfAsync(int invoiceId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().Transactions
            .AsNoTracking().Where(t => t.InvoiceId == invoiceId).OrderBy(t => t.Id).ToListAsync();
    }
}
