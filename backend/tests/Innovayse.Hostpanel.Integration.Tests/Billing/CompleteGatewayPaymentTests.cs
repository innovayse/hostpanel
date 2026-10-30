using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Billing.Commands.CompleteGatewayPayment;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.SDK.Plugins;
using Innovayse.SDK.Plugins.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Wolverine;
using Wolverine.Tracking;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// "Complete a hosted-gateway payment" (<c>CompleteGatewayPaymentCommand</c>, reached through
/// <c>POST /api/me/invoices/{id}/gateway-payment/complete</c> once
/// <c>CompleteMyGatewayPaymentHandler</c> settles ownership) against Postgres, with the payment
/// plugin replaced by <see cref="FakePaymentPluginResolver"/>.
/// </summary>
/// <remarks>
/// The two concurrent-writer races are kept as direct handler calls through a DI scope: making two real HTTP requests race
/// each other onto the same row reliably enough for a unit test is not achievable, so the race is
/// manufactured here instead, a second <see cref="Innovayse.Hostpanel.Infrastructure.Persistence.AppDbContext"/>
/// changing the row between this handler's read and its write, which is exactly what
/// <c>ConcurrencyConflictException</c> guards against. The ownership boundary itself is
/// <see cref="CompleteMyGatewayPaymentTests"/>'s.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CompleteGatewayPaymentTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Builds a host whose payment plugins come from <paramref name="resolver"/>.</summary>
    /// <param name="resolver">The fake resolver the host should resolve plugins through.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(FakePaymentPluginResolver resolver) =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IPaymentPluginResolver>(resolver)));

    /// <summary>Creates an invoice for <paramref name="clientId"/> and starts a gateway session on it.</summary>
    /// <param name="host">The host to create the invoice against.</param>
    /// <param name="client">The payer's authenticated client, used to start the session.</param>
    /// <param name="clientId">The client to invoice.</param>
    /// <param name="plugin">The fake plugin the session should resolve to.</param>
    /// <returns>The invoice id carrying a live gateway session.</returns>
    private async Task<int> CreateInvoiceWithSessionAsync(
        WebApplicationFactory<Program> host, HttpClient client, int clientId, FakePaymentPlugin plugin)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);

        plugin.CreatePaymentResult = new("gw-1", "https://pg.example/pay?mdOrder=gw-1");
        var started = await client.PostAsJsonAsync(
            $"/api/me/invoices/{invoiceId}/gateway-payment/start",
            new { module = "bank-gateway", returnUrl = BillingTestSeeds.AllowedReturnUrl });
        started.StatusCode.Should().Be(HttpStatusCode.OK);

        return invoiceId;
    }

    /// <summary>Completes a gateway payment for <paramref name="invoiceId"/>.</summary>
    /// <param name="client">The payer's authenticated client.</param>
    /// <param name="invoiceId">The invoice to complete.</param>
    /// <returns>The HTTP response.</returns>
    private static Task<HttpResponseMessage> CompleteAsync(HttpClient client, int invoiceId) =>
        client.PostAsync($"/api/me/invoices/{invoiceId}/gateway-payment/complete", content: null);

    /// <summary>A gateway that reports Paid marks the invoice paid with the gateway's own transaction id.</summary>
    [Fact]
    public async Task GatewaySaysPaidMarksPaidWithGatewayTransaction()
    {
        var plugin = new FakePaymentPlugin { StatusResult = new(GatewayPaymentState.Paid, "ref-9", "orderStatus:2", AmountMinor: 1000) };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "gw-complete-paid");
        var invoiceId = await CreateInvoiceWithSessionAsync(host, client, clientId, plugin);

        var response = await CompleteAsync(client, invoiceId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("state").GetString().Should().Be(nameof(GatewayCompletionState.Paid));

        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var fetchedBody = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        fetchedBody.RootElement.GetProperty("status").GetString().Should().Be(nameof(InvoiceStatus.Paid));
        fetchedBody.RootElement.GetProperty("payment").GetProperty("gatewayTransactionId").GetString().Should().Be("ref-9");

        // The client ledger row is written inline by the handler, once; no event handler repeats it.
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var ledger = await context.Transactions
            .Where(t => t.InvoiceId == invoiceId)
            .ToListAsync();
        ledger.Should().ContainSingle();
        ledger[0].TransactionId.Should().Be("ref-9");
        ledger[0].PaymentMethod.Should().Be("bank-gateway");
        ledger[0].AmountIn.Should().Be(10m);
    }

    /// <summary>
    /// A gateway that reports a paid amount other than what the invoice owes is refused with
    /// <c>GATEWAY_PAYMENT_MISMATCH</c> and the invoice stays unpaid.
    /// </summary>
    [Fact]
    public async Task GatewayReportingAnotherAmountIsRefused()
    {
        var plugin = new FakePaymentPlugin
        {
            StatusResult = new(GatewayPaymentState.Paid, "ref-9", "orderStatus:2", AmountMinor: 500),
        };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "gw-complete-mismatch");
        var invoiceId = await CreateInvoiceWithSessionAsync(host, client, clientId, plugin);

        var response = await CompleteAsync(client, invoiceId);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(HostpanelErrorCodes.GATEWAY_PAYMENT_MISMATCH);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var fetchedBody = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        fetchedBody.RootElement.GetProperty("status").GetString().Should().Be(nameof(InvoiceStatus.Unpaid));
    }

    /// <summary>A gateway that reports exactly the amount owed completes the payment.</summary>
    [Fact]
    public async Task GatewayReportingTheOwedAmountMarksPaid()
    {
        var plugin = new FakePaymentPlugin
        {
            StatusResult = new(GatewayPaymentState.Paid, "ref-9", "orderStatus:2", AmountMinor: 1000),
        };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "gw-complete-match");
        var invoiceId = await CreateInvoiceWithSessionAsync(host, client, clientId, plugin);

        var response = await CompleteAsync(client, invoiceId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("state").GetString().Should().Be(nameof(GatewayCompletionState.Paid));
    }

    /// <summary>An already-paid invoice answers Paid again without ever asking the gateway a second time.</summary>
    [Fact]
    public async Task AlreadyPaidIsIdempotentWithoutASecondGatewayCall()
    {
        var plugin = new FakePaymentPlugin { StatusResult = new(GatewayPaymentState.Paid, "ref-9", "orderStatus:2", AmountMinor: 1000) };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "gw-complete-idempotent");
        var invoiceId = await CreateInvoiceWithSessionAsync(host, client, clientId, plugin);
        (await CompleteAsync(client, invoiceId)).StatusCode.Should().Be(HttpStatusCode.OK);
        var callsAfterFirstComplete = plugin.GetStatusCallCount;

        var response = await CompleteAsync(client, invoiceId);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("state").GetString().Should().Be(nameof(GatewayCompletionState.Paid));
        plugin.GetStatusCallCount.Should().Be(callsAfterFirstComplete);
    }

    /// <summary>A gateway session still pending leaves the invoice unpaid and answers Pending.</summary>
    [Fact]
    public async Task PendingLeavesTheInvoiceUnpaid()
    {
        var plugin = new FakePaymentPlugin { StatusResult = new(GatewayPaymentState.Pending, null, "orderStatus:0") };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "gw-complete-pending");
        var invoiceId = await CreateInvoiceWithSessionAsync(host, client, clientId, plugin);

        var response = await CompleteAsync(client, invoiceId);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("state").GetString().Should().Be(nameof(GatewayCompletionState.Pending));

        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        var fetched = await admin.GetAsync($"/api/billing/{invoiceId}");
        using var fetchedBody = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        fetchedBody.RootElement.GetProperty("status").GetString().Should().Be(nameof(InvoiceStatus.Unpaid));
    }

    /// <summary>A declined gateway session leaves the invoice unpaid and answers Declined.</summary>
    [Fact]
    public async Task DeclinedLeavesTheInvoiceUnpaid()
    {
        var plugin = new FakePaymentPlugin { StatusResult = new(GatewayPaymentState.Declined, null, "orderStatus:6") };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "gw-complete-declined");
        var invoiceId = await CreateInvoiceWithSessionAsync(host, client, clientId, plugin);

        var response = await CompleteAsync(client, invoiceId);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("state").GetString().Should().Be(nameof(GatewayCompletionState.Declined));
    }

    /// <summary>
    /// Two simultaneous completions (the result page's auto-check racing the retry button, or the
    /// reconciler firing as the payer returns) can both pass the status guard. The loser's
    /// <c>SaveChangesAsync</c> hits the xmin concurrency token; the handler must answer
    /// <see cref="GatewayCompletionState.Paid"/> rather than surface that as a failure once the
    /// winner already recorded the payment.
    /// </summary>
    [Fact]
    public async Task ConcurrentCompletionAlreadyWonReturnsPaidWithoutThrowing()
    {
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync();
        var resolver = new FakePaymentPluginResolver();
        var plugin = new FakePaymentPlugin { StatusResult = new(GatewayPaymentState.Paid, "ref-9", "orderStatus:2", AmountMinor: 1000) };
        resolver.Register("bank-gateway", plugin);

        using var readerScope = factory.Services.CreateScope();
        var readerContext = readerScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = readerScope.ServiceProvider.GetRequiredService<IMessageBus>();
        var handler = new CompleteGatewayPaymentHandler(
            readerContext, resolver, bus, NullLogger<CompleteGatewayPaymentHandler>.Instance,
            readerScope.ServiceProvider.GetRequiredService<IClock>(),
            readerScope.ServiceProvider.GetRequiredService<IClientCredit>());

        // Loads the same row the handler is about to load, then wins the race by saving first.
        using (var winnerScope = factory.Services.CreateScope())
        {
            var winnerContext = winnerScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var winnerInvoice = await winnerContext.Invoices.FirstAsync(i => i.Id == invoiceId);
            winnerInvoice.Settle("bank-gateway", "ref-9", DateTimeOffset.UtcNow);
            await winnerContext.SaveChangesAsync(CancellationToken.None);
        }

        var result = await handler.HandleAsync(new CompleteGatewayPaymentCommand(invoiceId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.State.Should().Be(GatewayCompletionState.Paid);
    }

    /// <summary>
    /// A concurrency conflict from an unrelated writer, not the same payment already won, is not
    /// swallowed: the handler rethrows rather than answering <c>Paid</c> for a row that is still
    /// unpaid on reload.
    /// </summary>
    [Fact]
    public async Task ConcurrentCompletionOtherWriterDidNotWinRethrows()
    {
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync();
        var resolver = new FakePaymentPluginResolver();
        var plugin = new FakePaymentPlugin { StatusResult = new(GatewayPaymentState.Paid, "ref-9", "orderStatus:2", AmountMinor: 1000) };
        resolver.Register("bank-gateway", plugin);

        using var readerScope = factory.Services.CreateScope();
        var readerContext = readerScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var bus = readerScope.ServiceProvider.GetRequiredService<IMessageBus>();
        var handler = new CompleteGatewayPaymentHandler(
            readerContext, resolver, bus, NullLogger<CompleteGatewayPaymentHandler>.Instance,
            readerScope.ServiceProvider.GetRequiredService<IClock>(),
            readerScope.ServiceProvider.GetRequiredService<IClientCredit>());

        // The handler's own context has to be holding the row *before* the competing write, or it
        // saves against the current version and there is no conflict to test. Loading it here is
        // what the handler itself does moments later; EF hands back this tracked instance, still
        // carrying the pre-conflict row version.
        _ = await readerContext.Invoices.FirstAsync(i => i.Id == invoiceId);

        // An unrelated write against the same row bumps the row version without ever marking the
        // invoice paid, so the reload the handler does after the conflict must still see it unpaid
        // and rethrow rather than swallow the conflict.
        using (var otherWriterScope = factory.Services.CreateScope())
        {
            var otherContext = otherWriterScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var otherInvoice = await otherContext.Invoices.FirstAsync(i => i.Id == invoiceId);
            // A different session reference, not the seeded "gw-1": writing the same values back is
            // no change at all to EF, which then issues no UPDATE and leaves xmin where it was,
            // and the conflict this test exists to provoke never happens.
            otherInvoice.SetGatewaySession("bank-gateway", "gw-1-touched", otherWriterScope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
            await otherContext.SaveChangesAsync(CancellationToken.None);
        }

        var act = () => handler.HandleAsync(new CompleteGatewayPaymentCommand(invoiceId), CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
    }

    /// <summary>
    /// A payment an administrator records while the gateway session is open lowers what is due, so
    /// the gateway then takes more than the invoice still owes: the invoice settles on what is due
    /// and the rest goes to the client's credit balance instead of the payment being refused.
    /// </summary>
    [Fact]
    public async Task PaymentRecordedDuringTheSessionSettlesAndCreditsTheRest()
    {
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync();
        int clientId;
        using (var adminScope = factory.Services.CreateScope())
        {
            var adminContext = adminScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var invoice = await adminContext.Invoices.Include(i => i.Transactions).FirstAsync(i => i.Id == invoiceId);
            adminContext.Transactions.Add(invoice.AddPayment(
                DateTimeOffset.UtcNow, PaymentMethodNames.Manual, $"manual-{Guid.NewGuid():N}", 4m, 0m, null));
            await adminContext.SaveChangesAsync(CancellationToken.None);
            clientId = invoice.ClientId;
        }

        var resolver = new FakePaymentPluginResolver();
        var gatewayReference = $"ref-{Guid.NewGuid():N}";
        resolver.Register("bank-gateway", new FakePaymentPlugin
        {
            // The session was opened for the full 10.00 before the 4.00 was recorded.
            StatusResult = new(GatewayPaymentState.Paid, gatewayReference, "orderStatus:2", AmountMinor: 1000),
        });
        using var scope = factory.Services.CreateScope();
        var handler = new CompleteGatewayPaymentHandler(
            scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>(),
            resolver,
            scope.ServiceProvider.GetRequiredService<IMessageBus>(),
            NullLogger<CompleteGatewayPaymentHandler>.Instance,
            scope.ServiceProvider.GetRequiredService<IClock>(),
            scope.ServiceProvider.GetRequiredService<IClientCredit>());

        var result = await handler.HandleAsync(new CompleteGatewayPaymentCommand(invoiceId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.State.Should().Be(GatewayCompletionState.Paid);
        using var checkScope = factory.Services.CreateScope();
        var check = checkScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await check.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId)).Status.Should().Be(InvoiceStatus.Paid);
        (await check.Transactions.AsNoTracking().SingleAsync(t => t.TransactionId == gatewayReference)).AmountIn.Should().Be(6m);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(4m);
    }

    /// <summary>
    /// A paid status that reports no amount says nothing about what was taken: it is refused with
    /// <c>GATEWAY_PAYMENT_MISMATCH</c> and the invoice stays unpaid.
    /// </summary>
    [Fact]
    public async Task PaidWithoutAnAmountIsAMismatch()
    {
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync();
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", new FakePaymentPlugin
        {
            StatusResult = new(GatewayPaymentState.Paid, $"ref-{Guid.NewGuid():N}", "orderStatus:2"),
        });
        using var scope = factory.Services.CreateScope();

        var result = await HandlerFor(scope, resolver).HandleAsync(new CompleteGatewayPaymentCommand(invoiceId), CancellationToken.None);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.GATEWAY_PAYMENT_MISMATCH);
        using var checkScope = factory.Services.CreateScope();
        var check = checkScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await check.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId)).Status.Should().Be(InvoiceStatus.Unpaid);
    }

    /// <summary>
    /// An invoice another path settled while its session stayed open answers Paid, and what the
    /// session took, which no payment records, goes whole to the client's balance once: the replay
    /// credits nothing more.
    /// </summary>
    [Fact]
    public async Task SessionPaidAfterAnotherPathSettledCreditsTheWholeAmountOnce()
    {
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync();
        int clientId;
        using (var adminScope = factory.Services.CreateScope())
        {
            // Settled by another payment while the session's state is kept.
            var adminContext = adminScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var invoice = await adminContext.Invoices.Include(i => i.Transactions).FirstAsync(i => i.Id == invoiceId);
            adminContext.Transactions.Add(invoice.Settle(
                PaymentMethodNames.Manual, $"manual-{Guid.NewGuid():N}", DateTimeOffset.UtcNow)!);
            await adminContext.SaveChangesAsync(CancellationToken.None);
            clientId = invoice.ClientId;
        }

        var resolver = new FakePaymentPluginResolver();
        var gatewayReference = $"ref-{Guid.NewGuid():N}";
        resolver.Register("bank-gateway", new FakePaymentPlugin
        {
            StatusResult = new(GatewayPaymentState.Paid, gatewayReference, "orderStatus:2", AmountMinor: 1000),
        });

        using (var scope = factory.Services.CreateScope())
        {
            var first = await HandlerFor(scope, resolver).HandleAsync(new CompleteGatewayPaymentCommand(invoiceId), CancellationToken.None);
            first.Value!.State.Should().Be(GatewayCompletionState.Paid);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var replay = await HandlerFor(scope, resolver).HandleAsync(new CompleteGatewayPaymentCommand(invoiceId), CancellationToken.None);
            replay.Value!.State.Should().Be(GatewayCompletionState.Paid);
        }

        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(10m);
        using var checkScope = factory.Services.CreateScope();
        var check = checkScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var row = await check.Transactions.AsNoTracking().SingleAsync(t => t.TransactionId == gatewayReference);
        row.AddedToCredit.Should().BeTrue();
        row.AmountIn.Should().Be(10m);
        row.InvoiceId.Should().BeNull();
    }

    /// <summary>
    /// An administrator records the full payment while the client's hosted session is still open;
    /// the session keeps its state, so when the gateway later reports it paid the money is matched
    /// and goes whole to the client's balance, once, however often the completion is replayed.
    /// </summary>
    [Fact]
    public async Task AdminPaymentDuringAnOpenSessionThenGatewayPaidCreditsTheBalanceOnce()
    {
        var plugin = new FakePaymentPlugin { StatusResult = new(GatewayPaymentState.Pending, null, "orderStatus:0") };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "gw-admin-then-gateway");
        var invoiceId = await CreateInvoiceWithSessionAsync(host, client, clientId, plugin);
        var admin = await BillingTestSeeds.AdminClientAsync(factory, host);
        await BillingTestSeeds.MarkPaidAsync(admin, invoiceId, 10m);

        var gatewayReference = $"ref-{Guid.NewGuid():N}";
        plugin.StatusResult = new(GatewayPaymentState.Paid, gatewayReference, "orderStatus:2", AmountMinor: 1000);
        var first = await CompleteAsync(client, invoiceId);
        var replay = await CompleteAsync(client, invoiceId);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        replay.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("state").GetString()
            .Should().Be(nameof(GatewayCompletionState.Paid));
        (await BillingTestSeeds.CreditBalanceOfAsync(host, clientId)).Should().Be(10m);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var row = await context.Transactions.AsNoTracking().SingleAsync(t => t.TransactionId == gatewayReference);
        row.AddedToCredit.Should().BeTrue();
        row.AmountIn.Should().Be(10m);
        row.InvoiceId.Should().BeNull();
        (await context.Transactions.AsNoTracking().CountAsync(t => t.InvoiceId == invoiceId)).Should().Be(1);
    }

    /// <summary>
    /// An administrator cancelling the invoice between the handler's read and its save leaves the
    /// gateway's money unrecordable on it: the conflict is caught, the money goes to the client's
    /// balance under the gateway's reference, and the answer is <c>INVOICE_NOT_PAYABLE</c>.
    /// </summary>
    [Fact]
    public async Task ConcurrentCancelCreditsTheGatewayPayment()
    {
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync();
        var resolver = new FakePaymentPluginResolver();
        var gatewayReference = $"ref-{Guid.NewGuid():N}";
        resolver.Register("bank-gateway", new FakePaymentPlugin
        {
            StatusResult = new(GatewayPaymentState.Paid, gatewayReference, "orderStatus:2", AmountMinor: 1000),
        });
        using var readerScope = factory.Services.CreateScope();
        var readerContext = readerScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var handler = HandlerFor(readerScope, resolver);

        // The handler's context holds the pre-cancel row, so its save conflicts.
        var clientId = (await readerContext.Invoices.FirstAsync(i => i.Id == invoiceId)).ClientId;
        using (var adminScope = factory.Services.CreateScope())
        {
            var adminContext = adminScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            (await adminContext.Invoices.FirstAsync(i => i.Id == invoiceId)).Cancel();
            await adminContext.SaveChangesAsync(CancellationToken.None);
        }

        var result = await handler.HandleAsync(new CompleteGatewayPaymentCommand(invoiceId), CancellationToken.None);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(10m);
        using var checkScope = factory.Services.CreateScope();
        var check = checkScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await check.Transactions.AsNoTracking().SingleAsync(t => t.TransactionId == gatewayReference))
            .AddedToCredit.Should().BeTrue();
    }

    /// <summary>
    /// A gateway that took more than the invoice still owed (a payment was recorded during the
    /// session) puts the surplus on the client's balance and tells staff exactly once, after the save:
    /// the replay finds the settled invoice and its ledger row and publishes nothing.
    /// </summary>
    [Fact]
    public async Task SurplusCreditedToTheBalance_PublishesOneAlertAndNoneOnReplay()
    {
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync();
        await RecordManualPaymentAsync(invoiceId, 4m);
        var gatewayReference = $"ref-{Guid.NewGuid():N}";
        var resolver = PaidResolver(gatewayReference);

        var first = await CompleteTrackedAsync(invoiceId, resolver);
        var replay = await CompleteTrackedAsync(invoiceId, resolver);

        var alert = first.Should().ContainSingle().Which;
        alert.InvoiceId.Should().Be(invoiceId);
        alert.Module.Should().Be("bank-gateway");
        alert.Reference.Should().Be(gatewayReference);
        alert.Amount.Should().Be(4m);
        alert.Currency.Should().Be("USD");
        alert.Credited.Should().BeTrue();
        replay.Should().BeEmpty();
    }

    /// <summary>
    /// A session paid for an invoice another path already settled: the whole amount is kept on the
    /// client's balance, staff are told once with the amount credited, and a replay tells nobody.
    /// </summary>
    [Fact]
    public async Task PaymentForAnAlreadySettledInvoice_PublishesOneCreditedAlertAndNoneOnReplay()
    {
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync();
        await RecordManualPaymentAsync(invoiceId, 10m);
        var gatewayReference = $"ref-{Guid.NewGuid():N}";
        var resolver = PaidResolver(gatewayReference);

        var first = await CompleteTrackedAsync(invoiceId, resolver);
        var replay = await CompleteTrackedAsync(invoiceId, resolver);

        var alert = first.Should().ContainSingle().Which;
        alert.Amount.Should().Be(10m);
        alert.Reference.Should().Be(gatewayReference);
        alert.Credited.Should().BeTrue();
        replay.Should().BeEmpty();
    }

    /// <summary>
    /// Money the client's balance cannot take (a client billed in another currency than the charge) is
    /// held for an operator, and the alert says it was not credited. It is sent once, not on replay.
    /// </summary>
    [Fact]
    public async Task PaymentHeldForAnOperator_PublishesOneAlertMarkedNotCreditedAndNoneOnReplay()
    {
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync();
        await RecordManualPaymentAsync(invoiceId, 10m);
        using (var seed = factory.Services.CreateScope())
        {
            var context = seed.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var clientId = (await context.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId)).ClientId;
            (await context.Clients.SingleAsync(c => c.Id == clientId)).SetCurrency("EUR");
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var resolver = PaidResolver($"ref-{Guid.NewGuid():N}");

        var first = await CompleteTrackedAsync(invoiceId, resolver);
        var replay = await CompleteTrackedAsync(invoiceId, resolver);

        var alert = first.Should().ContainSingle().Which;
        alert.Amount.Should().Be(10m);
        alert.Credited.Should().BeFalse();
        replay.Should().BeEmpty();
    }

    /// <summary>A payment of exactly what is due leaves nothing over, so no alert is published.</summary>
    [Fact]
    public async Task ExactPayment_PublishesNoAlert()
    {
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync();

        var published = await CompleteTrackedAsync(invoiceId, PaidResolver($"ref-{Guid.NewGuid():N}"));

        published.Should().BeEmpty();
    }

    /// <summary>A resolver whose bank gateway reports the full 10.00 paid under a reference.</summary>
    /// <param name="reference">The gateway's reference of the payment.</param>
    /// <returns>The resolver.</returns>
    private static FakePaymentPluginResolver PaidResolver(string reference)
    {
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", new FakePaymentPlugin
        {
            StatusResult = new(GatewayPaymentState.Paid, reference, "orderStatus:2", AmountMinor: 1000),
        });
        return resolver;
    }

    /// <summary>Records a manual payment on an invoice, as staff would while the client's session is open.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="amount">The amount paid.</param>
    /// <returns>A task that completes once saved.</returns>
    private async Task RecordManualPaymentAsync(int invoiceId, decimal amount)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await context.Invoices.Include(i => i.Transactions).FirstAsync(i => i.Id == invoiceId);
        context.Transactions.Add(invoice.AddPayment(
            DateTimeOffset.UtcNow, PaymentMethodNames.Manual, $"manual-{Guid.NewGuid():N}", amount, 0m, null));
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Completes the invoice's gateway payment on a fresh scope and waits until the bus has handled
    /// everything the completion published.
    /// </summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="resolver">The fake plugin resolver.</param>
    /// <returns>The credited-payment alerts the completion published.</returns>
    private async Task<IReadOnlyList<GatewayPaymentCreditedEvent>> CompleteTrackedAsync(
        int invoiceId, FakePaymentPluginResolver resolver)
    {
        var session = await factory.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(async _ =>
            {
                using var scope = factory.Services.CreateScope();
                var result = await HandlerFor(scope, resolver).HandleAsync(new CompleteGatewayPaymentCommand(invoiceId), CancellationToken.None);
                result.IsSuccess.Should().BeTrue();
            }));
        return session.Executed.MessagesOf<GatewayPaymentCreditedEvent>().ToList();
    }

    /// <summary>Builds the handler on a scope's own context, with plugins from <paramref name="resolver"/>.</summary>
    /// <param name="scope">The DI scope whose context the handler uses.</param>
    /// <param name="resolver">The fake plugin resolver.</param>
    /// <returns>The handler.</returns>
    private static CompleteGatewayPaymentHandler HandlerFor(IServiceScope scope, FakePaymentPluginResolver resolver) =>
        new(
            scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>(),
            resolver,
            scope.ServiceProvider.GetRequiredService<IMessageBus>(),
            NullLogger<CompleteGatewayPaymentHandler>.Instance,
            scope.ServiceProvider.GetRequiredService<IClock>(),
            scope.ServiceProvider.GetRequiredService<IClientCredit>());

    /// <summary>
    /// Seeds a client and an invoice with a live gateway session directly against the database,
    /// bypassing HTTP, for the DI-scope tests that only need a row to race over.
    /// </summary>
    /// <returns>The seeded invoice's primary key.</returns>
    private async Task<int> SeedInvoiceWithSessionDirectlyAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var client = Client.Create(
            $"user-{Guid.NewGuid():N}", "Gateway", "Race", $"gw-race-{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);

        var invoice = Invoice.Create(client.Id, DateTimeOffset.UtcNow.AddDays(14), "USD", DateTimeOffset.UtcNow);
        invoice.AddItem("Hosting", 10m, 1);
        invoice.SetGatewaySession("bank-gateway", "gw-1", scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);

        return invoice.Id;
    }
}
