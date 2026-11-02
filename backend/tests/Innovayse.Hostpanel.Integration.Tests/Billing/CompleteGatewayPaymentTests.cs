using Innovayse.Hostpanel.Application.Billing.Common.Enums;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Billing.Commands.CompleteGatewayPayment;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Billing.Events;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Infrastructure;
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
        // A gateway reports one payment under one reference, and the ledger refuses a second, so each test names its own.
        var reference = $"ref-{Guid.NewGuid():N}";
        var plugin = new FakePaymentPlugin { StatusResult = new(GatewayPaymentState.Paid, reference, "orderStatus:2", AmountMinor: 1000) };
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
        fetchedBody.RootElement.GetProperty("payment").GetProperty("gatewayTransactionId").GetString().Should().Be(reference);

        // The client ledger row is written inline by the handler, once; no event handler repeats it.
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var ledger = await context.Transactions
            .Where(t => t.InvoiceId == invoiceId)
            .ToListAsync();
        ledger.Should().ContainSingle();
        ledger[0].TransactionId.Should().Be(reference);
        ledger[0].PaymentMethod.Should().Be("bank-gateway");
        ledger[0].AmountIn.Should().Be(10m);
        ledger[0].Source.Should().Be(TransactionSource.GatewayPayment);
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
        // A gateway reports one payment under one reference, and the ledger refuses a second, so each test names its own.
        var reference = $"ref-{Guid.NewGuid():N}";
        var plugin = new FakePaymentPlugin
        {
            StatusResult = new(GatewayPaymentState.Paid, reference, "orderStatus:2", AmountMinor: 1000),
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
        // A gateway reports one payment under one reference, and the ledger refuses a second, so each test names its own.
        var reference = $"ref-{Guid.NewGuid():N}";
        var plugin = new FakePaymentPlugin { StatusResult = new(GatewayPaymentState.Paid, reference, "orderStatus:2", AmountMinor: 1000) };
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
            winnerInvoice.Settle("bank-gateway", "ref-9", DateTimeOffset.UtcNow, TransactionSource.Manual);
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
    /// A gateway payment whose reference the ledger already holds for another payment is answered as a
    /// conflict (409), never as a server error: the unique index refuses the second row and nothing is
    /// recorded twice.
    /// </summary>
    [Fact]
    public async Task GatewayReferenceAlreadyOnTheLedgerForAnotherPayment_IsAConflictAndNothingIsRecordedTwice()
    {
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync("gw-dup-session");
        var reference = $"dup-{Guid.NewGuid():N}";
        using (var seedScope = factory.Services.CreateScope())
        {
            var seed = seedScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var clientId = await seed.Invoices.AsNoTracking().Where(i => i.Id == invoiceId).Select(i => i.ClientId).SingleAsync();
            seed.Transactions.Add(Transaction.Create(
                clientId, DateTimeOffset.UtcNow, "Earlier payment", reference, null, "bank-gateway", 10m, 0m, 0m, false, "USD",
                TransactionSource.GatewayPayment));
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", new FakePaymentPlugin
        {
            StatusResult = new(GatewayPaymentState.Paid, reference, "orderStatus:2", AmountMinor: 1000),
        });
        using var scope = factory.Services.CreateScope();

        var act = () => HandlerFor(scope, resolver).HandleAsync(new CompleteGatewayPaymentCommand(invoiceId), CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyConflictException>();
        using var check = factory.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Transactions.AsNoTracking().CountAsync(t => t.TransactionId == reference)).Should().Be(1);
        (await context.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId)).Status.Should().NotBe(InvoiceStatus.Paid);
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
                DateTimeOffset.UtcNow, PaymentMethodNames.Manual, $"manual-{Guid.NewGuid():N}", 4m, 0m, null, TransactionSource.Manual));
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
                PaymentMethodNames.Manual, $"manual-{Guid.NewGuid():N}", DateTimeOffset.UtcNow, TransactionSource.Manual)!);
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
        row.Source.Should().Be(TransactionSource.GatewayPayment);
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

    /// <summary>
    /// A gateway that reports paid with a null, empty or blank transaction id falls back to the session id
    /// the invoice holds: the hand-back is recorded under it, so a replay finds the row and credits
    /// nothing more and staff are alerted once.
    /// </summary>
    /// <param name="blankReference">The unusable transaction id the gateway reports.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task BlankGatewayTransactionId_FallsBackToTheSessionIdAndAReplayCreditsNothingMore(string? blankReference)
    {
        var sessionId = $"gw-{Guid.NewGuid():N}";
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync(sessionId);
        await RecordManualPaymentAsync(invoiceId, 10m);
        var clientId = await ClientOfAsync(invoiceId);
        var resolver = PaidResolver(blankReference);

        var first = await CompleteTrackedAsync(invoiceId, resolver);
        var balanceAfterFirst = await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId);
        var rowsAfterFirst = await LedgerRowCountAsync(clientId);
        var replay = await CompleteTrackedAsync(invoiceId, resolver);

        first.Should().ContainSingle().Which.Reference.Should().Be(sessionId);
        replay.Should().BeEmpty();
        balanceAfterFirst.Should().Be(10m);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(balanceAfterFirst);
        (await LedgerRowCountAsync(clientId)).Should().Be(rowsAfterFirst);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Transactions.AsNoTracking().CountAsync(t => t.TransactionId == sessionId)).Should().Be(1);
    }

    /// <summary>
    /// A paid report with no usable reference anywhere (the gateway sends none and the invoice's
    /// session id is empty or blank) is never credited: on an invoice another path settled the money
    /// is held for an operator and no ledger row is written, staff are alerted once, and a replay
    /// changes and sends nothing.
    /// </summary>
    /// <param name="sessionId">The unusable session id the invoice holds.</param>
    /// <param name="transactionId">The unusable transaction id the gateway reports.</param>
    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "  ")]
    [InlineData("", null)]
    public async Task NoReferenceAnywhereOnASettledInvoice_AlertsOnceAndWritesNothingOnFirstRunAndReplay(
        string sessionId, string? transactionId)
    {
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync(sessionId);
        await RecordManualPaymentAsync(invoiceId, 10m);
        var clientId = await ClientOfAsync(invoiceId);
        var resolver = PaidResolver(transactionId, "840");
        var rowsBefore = await LedgerRowCountAsync(clientId);

        var first = await CompleteTrackedWithAlertsAsync(invoiceId, resolver);
        var replay = await CompleteTrackedWithAlertsAsync(invoiceId, resolver);

        first.ErrorCode.Should().BeNull();
        var alert = first.Alerts.Should().ContainSingle().Which;
        alert.InvoiceId.Should().Be(invoiceId);
        alert.ClientId.Should().Be(clientId);
        alert.Module.Should().Be("bank-gateway");
        alert.Amount.Should().Be(10m);
        alert.Currency.Should().Be("USD");
        replay.Alerts.Should().BeEmpty();
        first.Credited.Should().BeEmpty();
        replay.Credited.Should().BeEmpty();
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
        (await LedgerRowCountAsync(clientId)).Should().Be(rowsBefore);
    }

    /// <summary>
    /// The same on an invoice that is still open: with no reference the payment is not settled or
    /// credited, the completion is refused as a mismatch, nothing is written but the alert's mark,
    /// staff are alerted once, and a replay is refused again without a second alert.
    /// </summary>
    /// <param name="sessionId">The unusable session id the invoice holds.</param>
    /// <param name="transactionId">The unusable transaction id the gateway reports.</param>
    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "  ")]
    [InlineData("", null)]
    public async Task NoReferenceAnywhereOnAnOpenInvoice_IsRefusedAndAlertsOnceOnFirstRunAndReplay(
        string sessionId, string? transactionId)
    {
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync(sessionId);
        var clientId = await ClientOfAsync(invoiceId);
        var resolver = PaidResolver(transactionId, "840");

        var first = await CompleteTrackedWithAlertsAsync(invoiceId, resolver);
        var replay = await CompleteTrackedWithAlertsAsync(invoiceId, resolver);

        first.ErrorCode.Should().Be(HostpanelErrorCodes.GATEWAY_PAYMENT_MISMATCH);
        replay.ErrorCode.Should().Be(HostpanelErrorCodes.GATEWAY_PAYMENT_MISMATCH);
        var alert = first.Alerts.Should().ContainSingle().Which;
        alert.InvoiceId.Should().Be(invoiceId);
        alert.ClientId.Should().Be(clientId);
        alert.Module.Should().Be("bank-gateway");
        alert.Amount.Should().Be(10m);
        alert.Currency.Should().Be("USD");
        replay.Alerts.Should().BeEmpty();
        using var check = factory.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var stored = await context.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId);
        stored.Status.Should().Be(InvoiceStatus.Unpaid);
        stored.GatewayUnreferencedNotifiedAt.Should().NotBeNull();
        (await LedgerRowCountAsync(clientId)).Should().Be(0);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
    }

    /// <summary>
    /// A session that already stalled has its stalled alert marked, yet a later paid report with no
    /// reference still alerts staff: the two alerts are tracked apart.
    /// </summary>
    [Fact]
    public async Task NoReferenceOnAnOpenInvoice_AfterTheStalledAlert_StillAlertsOnce()
    {
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync(string.Empty);
        using (var seed = factory.Services.CreateScope())
        {
            var context = seed.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            (await context.Invoices.SingleAsync(i => i.Id == invoiceId)).MarkGatewayStalledNotified(DateTimeOffset.UtcNow);
            await context.SaveChangesAsync(CancellationToken.None);
        }

        var first = await CompleteTrackedWithAlertsAsync(invoiceId, PaidResolver(null));

        first.Alerts.Should().ContainSingle();
    }

    /// <summary>
    /// An open invoice whose gateway reports no transaction id (null) settles under the session id the
    /// invoice holds, with no alert, and a replay finds the settled invoice and changes nothing.
    /// </summary>
    [Fact]
    public async Task OpenInvoice_WithANullTransactionId_SettlesUnderTheSessionIdAndAReplayChangesNothing()
    {
        var sessionId = $"gw-{Guid.NewGuid():N}";
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync(sessionId);
        var clientId = await ClientOfAsync(invoiceId);
        var resolver = PaidResolver(null);

        var first = await CompleteTrackedWithAlertsAsync(invoiceId, resolver);
        var rowsAfterFirst = await LedgerRowCountAsync(clientId);
        var replay = await CompleteTrackedWithAlertsAsync(invoiceId, resolver);

        first.ErrorCode.Should().BeNull();
        replay.ErrorCode.Should().BeNull();
        first.Alerts.Should().BeEmpty();
        replay.Alerts.Should().BeEmpty();
        rowsAfterFirst.Should().Be(1);
        (await LedgerRowCountAsync(clientId)).Should().Be(rowsAfterFirst);
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId)).Status.Should().Be(InvoiceStatus.Paid);
        (await context.Transactions.AsNoTracking().CountAsync(t => t.TransactionId == sessionId && t.InvoiceId == invoiceId)).Should().Be(1);
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
    }

    /// <summary>
    /// The amount in the alert is the gateway's own figure, so it is given only when the gateway
    /// reports the invoice's currency: another currency, or none, leaves the amount out rather than
    /// printing a wrong one.
    /// </summary>
    /// <param name="currencyNumeric">The numeric currency code the gateway reports, or null for none.</param>
    [Theory]
    [InlineData("978")]
    [InlineData(null)]
    public async Task NoReferenceOnASettledInvoice_WithAnotherOrUnknownGatewayCurrency_LeavesTheAmountOut(string? currencyNumeric)
    {
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync(string.Empty);
        await RecordManualPaymentAsync(invoiceId, 10m);

        var run = await CompleteTrackedWithAlertsAsync(invoiceId, PaidResolver(null, currencyNumeric));

        var alert = run.Alerts.Should().ContainSingle().Which;
        alert.Amount.Should().BeNull();
        alert.Currency.Should().Be("USD");
    }

    /// <summary>The session id the invoice holds is trimmed too when it stands in for a missing transaction id.</summary>
    [Fact]
    public async Task OpenInvoice_WithAPaddedSessionIdAndNoTransactionId_SettlesUnderTheTrimmedSessionId()
    {
        var sessionId = $"gw-{Guid.NewGuid():N}";
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync($"  {sessionId} ");

        var run = await CompleteTrackedWithAlertsAsync(invoiceId, PaidResolver(null));

        run.ErrorCode.Should().BeNull();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Transactions.AsNoTracking().CountAsync(t => t.TransactionId == sessionId)).Should().Be(1);
    }

    /// <summary>
    /// When the save of the alert's mark loses to another writer nothing is published, the refusal is
    /// still answered, and the next pass finds no mark and alerts staff exactly once.
    /// </summary>
    [Fact]
    public async Task ConcurrentWriterDuringTheAlertMark_PublishesNothingAndTheNextPassAlertsOnce()
    {
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync(string.Empty);
        var resolver = PaidResolver(null);
        string? errorCode = null;

        var conflicted = await factory.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(async bus =>
            {
                using var readerScope = factory.Services.CreateScope();

                // The handler's context holds the row before the competing write, so its save runs
                // against the old row version and conflicts (the handler loads this same instance).
                _ = await readerScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().Invoices.FirstAsync(i => i.Id == invoiceId);
                using (var otherScope = factory.Services.CreateScope())
                {
                    var otherContext = otherScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
                    var otherInvoice = await otherContext.Invoices.FirstAsync(i => i.Id == invoiceId);

                    // A different, still blank, session id: writing the same values back is no change to EF.
                    otherInvoice.SetGatewaySession("bank-gateway", "   ", otherScope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
                    await otherContext.SaveChangesAsync(CancellationToken.None);
                }

                errorCode = (await HandlerFor(readerScope, resolver).HandleAsync(new CompleteGatewayPaymentCommand(invoiceId), CancellationToken.None)).ErrorCode;
            }));

        errorCode.Should().Be(HostpanelErrorCodes.GATEWAY_PAYMENT_MISMATCH);
        conflicted.Executed.MessagesOf<GatewayPaymentWithoutReferenceEvent>().Should().BeEmpty();
        var next = await CompleteTrackedWithAlertsAsync(invoiceId, resolver);
        var after = await CompleteTrackedWithAlertsAsync(invoiceId, resolver);
        next.Alerts.Should().ContainSingle();
        after.Alerts.Should().BeEmpty();
    }

    /// <summary>A reference with surrounding whitespace is stored trimmed, so a replay looks up what was stored.</summary>
    [Fact]
    public async Task ReferenceWithSurroundingWhitespace_IsStoredTrimmed()
    {
        var reference = $"ref-{Guid.NewGuid():N}";
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync();

        await CompleteTrackedWithAlertsAsync(invoiceId, PaidResolver($"  {reference} "));

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Transactions.AsNoTracking().CountAsync(t => t.TransactionId == reference)).Should().Be(1);
    }

    /// <summary>
    /// The conflict path (the invoice was cancelled between the read and the save) hands the gateway's
    /// money back through the balance and publishes the credited alert exactly once, after the save.
    /// </summary>
    [Fact]
    public async Task ConcurrentCancel_PublishesTheCreditedAlertExactlyOnce()
    {
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync();
        var reference = $"ref-{Guid.NewGuid():N}";
        using var readerScope = factory.Services.CreateScope();
        var readerContext = readerScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var handler = HandlerFor(readerScope, PaidResolver(reference));

        // The handler's context holds the pre-cancel row, so its settle save conflicts.
        _ = await readerContext.Invoices.FirstAsync(i => i.Id == invoiceId);
        await CancelAsync(invoiceId);

        var published = await PublishedDuringAsync(factory.Services, async () =>
        {
            var result = await handler.HandleAsync(new CompleteGatewayPaymentCommand(invoiceId), CancellationToken.None);
            result.ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        });

        var alert = published.Should().ContainSingle().Which;
        alert.InvoiceId.Should().Be(invoiceId);
        alert.Reference.Should().Be(reference);
        alert.Amount.Should().Be(10m);
        alert.Credited.Should().BeTrue();
    }

    /// <summary>
    /// When the hand-back's own save is lost (the client's balance moved underneath it), nothing is
    /// published: no ledger row, no credit and no alert, because the event follows the save.
    /// </summary>
    [Fact]
    public async Task ConcurrentCancel_WhenTheHandBackSaveIsLost_PublishesNothing()
    {
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync();
        var reference = $"ref-{Guid.NewGuid():N}";
        int clientId;
        using (var seed = factory.Services.CreateScope())
        {
            clientId = (await seed.ServiceProvider.GetRequiredService<IHostpanelDbContext>().Invoices
                .AsNoTracking().SingleAsync(i => i.Id == invoiceId)).ClientId;
        }

        // Fires as the hand-back saves its credit: another writer moves the client's balance first.
        var hook = new CreditSaveHookInterceptor(async () =>
        {
            using var other = factory.Services.CreateScope();
            (await other.ServiceProvider.GetRequiredService<IClientCredit>()
                .AddAndSaveAsync(new ClientCreditChange(clientId, 1m), CancellationToken.None)).IsSuccess.Should().BeTrue();
        });
        await using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(hook))));
        using var readerScope = host.Services.CreateScope();
        var readerContext = readerScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var handler = HandlerFor(readerScope, PaidResolver(reference));
        _ = await readerContext.Invoices.FirstAsync(i => i.Id == invoiceId);
        await CancelAsync(invoiceId);

        var published = await PublishedDuringAsync(host.Services, async () =>
        {
            var act = () => handler.HandleAsync(new CompleteGatewayPaymentCommand(invoiceId), CancellationToken.None);
            await act.Should().ThrowAsync<ConcurrencyConflictException>();
        });

        hook.Fired.Should().BeTrue("the balance must have moved while the hand-back was saving");
        published.Should().BeEmpty();
        using var checkScope = factory.Services.CreateScope();
        var check = checkScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await check.Transactions.AsNoTracking().AnyAsync(t => t.TransactionId == reference)).Should().BeFalse();
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(1m);
    }

    /// <summary>
    /// The payment hand-back of a settled or closed invoice is saved while another request records the
    /// same gateway payment: the unique gateway payment index refuses the second row, and the hand-back
    /// stands down. The ledger keeps the one row the other request wrote, nothing is credited twice and
    /// staff are not alerted a second time.
    /// </summary>
    [Fact]
    public async Task HandBackUnrecorded_WhenTheLedgerHoldsThePaymentAfterTheSaveIsRefused_StandsDownQuietly()
    {
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync("gw-standdown-session");
        var clientId = await ClientOfAsync(invoiceId);
        var reference = $"ref-{Guid.NewGuid():N}";

        // The other request records the same gateway payment right before this hand-back saves its row.
        var hook = new LedgerRowSaveHookInterceptor(
            row => row.TransactionId == reference && row.InvoiceId is null,
            async () =>
            {
                using var other = factory.Services.CreateScope();
                var otherContext = other.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
                otherContext.Transactions.Add(Transaction.Create(
                    clientId, DateTimeOffset.UtcNow, "Gateway payment held for operator review", reference, null, "bank-gateway",
                    10m, 0m, 0m, false, "USD", TransactionSource.GatewayPayment));
                await otherContext.SaveChangesAsync(CancellationToken.None);
            });
        await using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(hook))));
        using var readerScope = host.Services.CreateScope();
        var readerContext = readerScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var handler = HandlerFor(readerScope, PaidResolver(reference));

        // The handler's context holds the pre-cancel row, so its settle save conflicts and the hand-back runs.
        _ = await readerContext.Invoices.FirstAsync(i => i.Id == invoiceId);
        await CancelAsync(invoiceId);

        var published = await PublishedDuringAsync(host.Services, async () =>
        {
            var result = await handler.HandleAsync(new CompleteGatewayPaymentCommand(invoiceId), CancellationToken.None);
            result.ErrorCode.Should().Be(HostpanelErrorCodes.INVOICE_NOT_PAYABLE);
        });

        hook.Fired.Should().BeTrue("the other request must have recorded the payment while the hand-back was saving");
        published.Should().BeEmpty();
        using var checkScope = factory.Services.CreateScope();
        var check = checkScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var row = await check.Transactions.AsNoTracking().SingleAsync(t => t.TransactionId == reference);
        row.Description.Should().Be("Gateway payment held for operator review");
        (await BillingTestSeeds.CreditBalanceOfAsync(factory, clientId)).Should().Be(0m);
    }

    /// <summary>
    /// A hand-back whose save is refused for a reason other than the payment being on the ledger (here
    /// the client's balance moved) is not a stand-down: the ledger holds no row for the payment, so the
    /// conflict reaches the caller and nothing is recorded or alerted.
    /// </summary>
    [Fact]
    public async Task HandBackUnrecorded_WhenTheSaveIsRefusedAndNoRowIsOnTheLedger_TheConflictPropagatesAndNothingIsRecorded()
    {
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync("gw-nostanddown-session");
        var clientId = await ClientOfAsync(invoiceId);
        var reference = $"ref-{Guid.NewGuid():N}";
        var hook = new LedgerRowSaveHookInterceptor(
            row => row.TransactionId == reference && row.InvoiceId is null,
            async () =>
            {
                using var other = factory.Services.CreateScope();
                (await other.ServiceProvider.GetRequiredService<IClientCredit>()
                    .AddAndSaveAsync(new ClientCreditChange(clientId, 1m), CancellationToken.None)).IsSuccess.Should().BeTrue();
            });
        await using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.ConfigureDbContext<AppDbContext>(options => options.AddInterceptors(hook))));
        using var readerScope = host.Services.CreateScope();
        var readerContext = readerScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var handler = HandlerFor(readerScope, PaidResolver(reference));
        _ = await readerContext.Invoices.FirstAsync(i => i.Id == invoiceId);
        await CancelAsync(invoiceId);

        var published = await PublishedDuringAsync(host.Services, async () =>
        {
            var act = () => handler.HandleAsync(new CompleteGatewayPaymentCommand(invoiceId), CancellationToken.None);
            await act.Should().ThrowAsync<ConcurrencyConflictException>();
        });

        hook.Fired.Should().BeTrue();
        published.Should().BeEmpty();
        using var checkScope = factory.Services.CreateScope();
        var check = checkScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await check.Transactions.AsNoTracking().AnyAsync(t => t.TransactionId == reference)).Should().BeFalse();
    }

    /// <summary>
    /// A completion that loses the race to a request which already recorded the very same gateway
    /// payment (the invoice settled, its ledger row written) answers <see cref="GatewayCompletionState.Paid"/>
    /// and records nothing a second time.
    /// </summary>
    [Fact]
    public async Task ConcurrentCompletion_WhenTheWinnerRecordedThePaymentOnTheLedger_TheLoserAnswersPaidAndRecordsNothingMore()
    {
        var invoiceId = await SeedInvoiceWithSessionDirectlyAsync("gw-winner-session");
        var reference = $"ref-{Guid.NewGuid():N}";
        using var loserScope = factory.Services.CreateScope();
        var loserContext = loserScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var loser = HandlerFor(loserScope, PaidResolver(reference));

        // The loser's context holds the open invoice, so its own save conflicts once the winner has committed.
        _ = await loserContext.Invoices.Include(i => i.Transactions).FirstAsync(i => i.Id == invoiceId);
        using (var winnerScope = factory.Services.CreateScope())
        {
            var winnerContext = winnerScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var winnerInvoice = await winnerContext.Invoices.Include(i => i.Transactions).FirstAsync(i => i.Id == invoiceId);
            winnerContext.Transactions.Add(winnerInvoice.Settle(
                "bank-gateway", reference, DateTimeOffset.UtcNow, TransactionSource.GatewayPayment)!);
            await winnerContext.SaveChangesAsync(CancellationToken.None);
        }

        var published = await PublishedDuringAsync(factory.Services, async () =>
        {
            var result = await loser.HandleAsync(new CompleteGatewayPaymentCommand(invoiceId), CancellationToken.None);
            result.IsSuccess.Should().BeTrue();
            result.Value!.State.Should().Be(GatewayCompletionState.Paid);
        });

        published.Should().BeEmpty();
        using var checkScope = factory.Services.CreateScope();
        var check = checkScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await check.Transactions.AsNoTracking().CountAsync(t => t.TransactionId == reference)).Should().Be(1);
        (await check.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId)).Status.Should().Be(InvoiceStatus.Paid);
    }

    /// <summary>
    /// A gateway reference that the ledger already holds for another payment answers 409 with the
    /// standard problem body over HTTP, not a server error, and the payment is not recorded twice.
    /// </summary>
    [Fact]
    public async Task GatewayReferenceAlreadyOnTheLedgerForAnotherPayment_AnswersConflictWithTheStandardErrorBody()
    {
        var reference = $"ref-{Guid.NewGuid():N}";
        var plugin = new FakePaymentPlugin { StatusResult = new(GatewayPaymentState.Paid, reference, "orderStatus:2", AmountMinor: 1000) };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        var (client, clientId) = await BillingTestSeeds.RegisterClientAsync(factory, host, "gw-complete-dup-reference");
        var invoiceId = await CreateInvoiceWithSessionAsync(host, client, clientId, plugin);
        using (var seedScope = host.Services.CreateScope())
        {
            var seed = seedScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            seed.Transactions.Add(Transaction.Create(
                clientId, DateTimeOffset.UtcNow, "Earlier payment", reference, null, "bank-gateway", 10m, 0m, 0m, false, "USD",
                TransactionSource.GatewayPayment));
            await seed.SaveChangesAsync(CancellationToken.None);
        }

        var response = await CompleteAsync(client, invoiceId);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("type").GetString().Should().Be(SharedErrorCodes.Conflict);
        body.GetProperty("status").GetInt32().Should().Be(409);
        body.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Transactions.AsNoTracking().CountAsync(t => t.TransactionId == reference)).Should().Be(1);
        (await context.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoiceId)).Status.Should().NotBe(InvoiceStatus.Paid);
    }

    /// <summary>The id of the client an invoice belongs to.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The client id.</returns>
    private async Task<int> ClientOfAsync(int invoiceId)
    {
        using var scope = factory.Services.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().Invoices
            .AsNoTracking().SingleAsync(i => i.Id == invoiceId)).ClientId;
    }

    /// <summary>The number of ledger rows a client has.</summary>
    /// <param name="clientId">The client.</param>
    /// <returns>The row count.</returns>
    private async Task<int> LedgerRowCountAsync(int clientId)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>().Transactions
            .AsNoTracking().CountAsync(t => t.ClientId == clientId);
    }

    /// <summary>Cancels an invoice from a scope of its own, as an administrator would.</summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>A task that completes once saved.</returns>
    private async Task CancelAsync(int invoiceId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        (await context.Invoices.FirstAsync(i => i.Id == invoiceId)).Cancel();
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Runs an action and waits until the bus has handled everything it published.</summary>
    /// <param name="services">The services of the host whose bus the action publishes through.</param>
    /// <param name="action">What to run.</param>
    /// <returns>The credited-payment alerts published meanwhile.</returns>
    private static async Task<IReadOnlyList<GatewayPaymentCreditedEvent>> PublishedDuringAsync(
        IServiceProvider services, Func<Task> action)
    {
        var session = await services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(_ => action()));
        return session.Executed.MessagesOf<GatewayPaymentCreditedEvent>().ToList();
    }

    /// <summary>
    /// Completes the invoice's gateway payment on a fresh scope and waits until the bus has handled
    /// everything the completion published, keeping the answer and both kinds of alert.
    /// </summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="resolver">The fake plugin resolver.</param>
    /// <returns>The error code the completion answered (null on success) and the alerts it published.</returns>
    private async Task<(string? ErrorCode, IReadOnlyList<GatewayPaymentWithoutReferenceEvent> Alerts, IReadOnlyList<GatewayPaymentCreditedEvent> Credited)> CompleteTrackedWithAlertsAsync(
        int invoiceId, FakePaymentPluginResolver resolver)
    {
        string? errorCode = null;
        var session = await factory.Services.GetRequiredService<IHost>().TrackActivity()
            .Timeout(TimeSpan.FromSeconds(60))
            .ExecuteAndWaitAsync((Func<IMessageContext, Task>)(async _ =>
            {
                using var scope = factory.Services.CreateScope();
                var result = await HandlerFor(scope, resolver).HandleAsync(new CompleteGatewayPaymentCommand(invoiceId), CancellationToken.None);
                errorCode = result.ErrorCode;
            }));
        return (
            errorCode,
            session.Executed.MessagesOf<GatewayPaymentWithoutReferenceEvent>().ToList(),
            session.Executed.MessagesOf<GatewayPaymentCreditedEvent>().ToList());
    }

    /// <summary>A resolver whose bank gateway reports the full 10.00 paid under a reference.</summary>
    /// <param name="reference">The gateway's reference of the payment.</param>
    /// <param name="currencyNumeric">The numeric currency code the gateway reports, or <see langword="null"/> when it reports none.</param>
    /// <returns>The resolver.</returns>
    private static FakePaymentPluginResolver PaidResolver(string? reference, string? currencyNumeric = null)
    {
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", new FakePaymentPlugin
        {
            StatusResult = new(GatewayPaymentState.Paid, reference, "orderStatus:2", AmountMinor: 1000, Currency: currencyNumeric),
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
            DateTimeOffset.UtcNow, PaymentMethodNames.Manual, $"manual-{Guid.NewGuid():N}", amount, 0m, null, TransactionSource.Manual));
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
        return await PublishedDuringAsync(factory.Services, async () =>
        {
            using var scope = factory.Services.CreateScope();
            var result = await HandlerFor(scope, resolver).HandleAsync(new CompleteGatewayPaymentCommand(invoiceId), CancellationToken.None);
            result.IsSuccess.Should().BeTrue();
        });
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
    /// <param name="sessionId">The gateway session id stored on the invoice; unique per test when the ledger is asserted on.</param>
    /// <returns>The seeded invoice's primary key.</returns>
    private async Task<int> SeedInvoiceWithSessionDirectlyAsync(string sessionId = "gw-1")
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();

        var client = Client.Create(
            $"user-{Guid.NewGuid():N}", "Gateway", "Race", $"gw-race-{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);

        var invoice = Invoice.Create(client.Id, DateTimeOffset.UtcNow.AddDays(14), "USD", DateTimeOffset.UtcNow);
        invoice.AddItem("Hosting", 10m, 1);
        invoice.SetGatewaySession("bank-gateway", sessionId, scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);

        return invoice.Id;
    }
}
