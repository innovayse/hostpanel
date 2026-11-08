using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Orders.Commands.FulfillPaidOrder;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Products;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Infrastructure.Integrations.NameAm;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <see cref="FulfillPaidOrderHandler"/> against Postgres: the not-found and not-invoiced
/// refusals, the idempotent no-op, the plain-service happy path and the automatic refund of a
/// rejected domain line, through the hosted gateway plugin.
/// </summary>
/// <remarks>
/// The handler is built by hand from a DI scope rather than resolved from one, the same way
/// <c>CompleteGatewayPaymentTests</c> does for its own concurrency cases: Wolverine invokes
/// handlers itself and does not register them as resolvable services.
/// </remarks>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class FulfillPaidOrderTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Builds the handler over a DI scope's real ports.</summary>
    /// <param name="scope">The scope to resolve the context and bus from.</param>
    /// <param name="logger">The logger the handler writes to; a null logger when the test reads no log.</param>
    /// <returns>The handler under test.</returns>
    private static FulfillPaidOrderHandler BuildHandler(IServiceScope scope, ILogger<FulfillPaidOrderHandler>? logger = null) =>
        new(
            scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>(),
            scope.ServiceProvider.GetRequiredService<IMessageBus>(),
            logger ?? NullLogger<FulfillPaidOrderHandler>.Instance,
            scope.ServiceProvider.GetRequiredService<IClock>(),
            scope.ServiceProvider.GetRequiredService<IClientCredit>());

    /// <summary>
    /// Builds a host whose refund command reaches <paramref name="pluginResolver"/> instead of a real gateway.
    /// </summary>
    /// <param name="pluginResolver">The fake plugin resolver the refund resolves hosted gateways through.</param>
    /// <param name="useRealRegistrar">Whether domain registration goes to the real, unconfigured registrar provider.</param>
    /// <param name="configure">Further changes to the host's services, for a test that needs another fake.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(
        FakePaymentPluginResolver pluginResolver, bool useRealRegistrar = false, Action<IServiceCollection>? configure = null) =>
        factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddSingleton<IPaymentPluginResolver>(pluginResolver);
            if (useRealRegistrar)
            {
                s.AddScoped<IRegistrarProvider, NameAmRegistrarProvider>();
            }

            configure?.Invoke(s);
        }));

    /// <summary>Seeds a domain-registration line for a name already registered here, so the registrar refuses it.</summary>
    /// <param name="context">The context to seed through.</param>
    /// <param name="clientId">The ordering client.</param>
    /// <param name="lineAmount">The domain line's first payment.</param>
    /// <param name="serviceProductId">When set, a hosting line for this product is added beside the domain line.</param>
    /// <returns>The order id and its invoice.</returns>
    private static async Task<RejectedDomainOrder> SeedRejectedDomainOrderAsync(
        IHostpanelDbContext context, int clientId, decimal lineAmount, int? serviceProductId = null)
    {
        var domainName = $"taken-{Guid.NewGuid():N}"[..18] + ".example";
        var holderId = await DomainsTestSeeds.SeedClientAsync(context);
        var domainProductId = await DomainsTestSeeds.SeedHostingProductAsync(context);
        context.Domains.Add(RegisteredDomain.Register(
            holderId, domainName, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), autoRenew: false, whoisPrivacy: false, priceCurrency: TestCurrencies.Base));
        await context.SaveChangesAsync(CancellationToken.None);

        var orderId = await SeedPendingOrderAsync(
            context, clientId,
            order =>
            {
                order.AddItem(
                    domainProductId, domainName, BillingCycle.Annual, lineAmount, lineAmount, domain: domainName, hostname: null,
                    domainAction: DomainAction.Register, years: 1);
                if (serviceProductId is not null)
                {
                    order.AddItem(
                        serviceProductId.Value, "Starter Hosting", BillingCycle.Monthly,
                        25m - lineAmount, 25m - lineAmount, domain: null, hostname: null);
                }
            });

        var order = await context.Orders.FindAsync(orderId);
        var invoice = await context.Invoices.FindAsync(order!.InvoiceId!.Value);
        return new(orderId, invoice!);
    }

    /// <summary>Seeds a client and an active hosting product priced in USD.</summary>
    /// <param name="context">The context to seed through.</param>
    /// <returns>The client id and product id.</returns>
    private static async Task<ClientProductIds> SeedClientAndProductAsync(IHostpanelDbContext context)
    {
        var client = Client.Create($"subject-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        context.Clients.Add(client);

        var group = ProductGroup.Create($"fulfill-{Guid.NewGuid():N}", null);
        context.ProductGroups.Add(group);
        await context.SaveChangesAsync(CancellationToken.None);

        var product = Product.Create(
            group.Id, "Starter Hosting", null, null, null, null, ProductType.SharedHosting, 0m, 0m, DateTimeOffset.UtcNow);
        context.Products.Add(product);
        await context.SaveChangesAsync(CancellationToken.None);

        return new(client.Id, product.Id);
    }

    /// <summary>Seeds a Pending order with a linked, unpaid invoice.</summary>
    /// <param name="context">The context to seed through.</param>
    /// <param name="clientId">The owning client.</param>
    /// <param name="configureItems">Adds the order's line items.</param>
    /// <returns>The order id.</returns>
    private static async Task<int> SeedPendingOrderAsync(
        IHostpanelDbContext context, int clientId, Action<Order> configureItems)
    {
        var order = Order.Create($"ORD-{Guid.NewGuid():N}"[..12], clientId, "bank-gateway", ipAddress: null, currency: "USD", now: DateTimeOffset.UtcNow);
        configureItems(order);
        context.Orders.Add(order);

        var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
        invoice.AddItem("Order", 25m, 1);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);

        order.LinkInvoice(invoice.Id);
        await context.SaveChangesAsync(CancellationToken.None);

        return order.Id;
    }

    /// <summary>An id nothing names is refused as <c>ORDER_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task MissingOrderIsRefused()
    {
        using var scope = factory.Services.CreateScope();
        var handler = BuildHandler(scope);

        var result = await handler.HandleAsync(new FulfillPaidOrderCommand(987_654_321), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_NOT_FOUND);
    }

    /// <summary>
    /// An order with no linked invoice is refused as <c>ORDER_NOT_INVOICED</c> before it is
    /// accepted, so it stays Pending for a later, real payment.
    /// </summary>
    [Fact]
    public async Task UninvoicedOrderIsRefusedAndStaysPending()
    {
        var seeded = await OrdersTestSeeds.SeedUninvoicedOrderAsync(factory);
        using var scope = factory.Services.CreateScope();

        var result = await BuildHandler(scope).HandleAsync(new FulfillPaidOrderCommand(seeded.OrderId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_NOT_INVOICED);
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var order = await context.Orders.AsNoTracking().SingleAsync(o => o.Id == seeded.OrderId, CancellationToken.None);
        order.Status.Should().Be(OrderStatus.Pending);
    }

    /// <summary>An order that already ran fulfilment (no longer Pending) is a no-op, and still succeeds.</summary>
    [Fact]
    public async Task AlreadyAcceptedOrderIsANoOp()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, productId) = await SeedClientAndProductAsync(context);
        var orderId = await SeedPendingOrderAsync(
            context, clientId,
            order => order.AddItem(productId, "Starter Hosting", BillingCycle.Monthly, 10m, 10m, domain: null, hostname: null));
        var order = await context.Orders.FindAsync(orderId);
        order!.Accept(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        var handler = BuildHandler(scope);
        var result = await handler.HandleAsync(new FulfillPaidOrderCommand(orderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>A Pending order with a plain hosting line is accepted and provisions a client service.</summary>
    [Fact]
    public async Task PendingOrderWithAServiceItemAcceptsAndCreatesTheService()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, productId) = await SeedClientAndProductAsync(context);
        var orderId = await SeedPendingOrderAsync(
            context, clientId,
            order => order.AddItem(productId, "Starter Hosting", BillingCycle.Monthly, 10m, 10m, domain: null, hostname: null));

        var handler = BuildHandler(scope);
        var result = await handler.HandleAsync(new FulfillPaidOrderCommand(orderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        // Read from the database, not from the tracked copy: the lines are fulfilled and the status settled in the
        // bus's own scope, so the copy this scope loaded at the accept still says Incomplete.
        var reloaded = await context.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
        reloaded.Status.Should().Be(OrderStatus.Active);

        var created = await context.ClientServices
            .Where(s => s.ClientId == clientId)
            .ToListAsync(CancellationToken.None);
        created.Should().ContainSingle(s => s.ProductId == productId);
    }

    /// <summary>
    /// A registration line naming a domain already registered here is refused by the registrar
    /// gate before any network call, and the line's amount is refunded through the invoice's
    /// hosted gateway plugin, with the refund and its ledger row recorded.
    /// </summary>
    [Fact]
    public async Task DomainAlreadyRegisteredRefundsThePluginPayment()
    {
        var plugin = new FakePaymentPlugin { RefundResult = "plugin-refund-1" };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, _) = await SeedClientAndProductAsync(context);
        var (orderId, invoice) = await SeedRejectedDomainOrderAsync(context, clientId, lineAmount: 25m);
        invoice.SetGatewaySession("bank-gateway", "gw-order-refund-1", scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
        invoice.Settle("bank-gateway", "pre-refund-txn", DateTimeOffset.UtcNow, TransactionSource.Manual);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await BuildHandler(scope).HandleAsync(new FulfillPaidOrderCommand(orderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        plugin.LastRefundGatewayOrderId.Should().Be("gw-order-refund-1");
        plugin.LastRefundAmountMinor.Should().Be(2500);
        var ledger = await context.Transactions.AsNoTracking()
            .Where(t => t.InvoiceId == invoice.Id && t.AmountOut > 0).ToListAsync(CancellationToken.None);
        ledger.Should().ContainSingle(t => t.AmountOut == 25m && t.TransactionId == "plugin-refund-1");

        // The refunded line is closed, so no retry can deliver what was given back, and it no longer keeps the order incomplete.
        var order = await context.Orders.AsNoTracking().Include(o => o.Items).SingleAsync(o => o.Id == orderId, CancellationToken.None);
        order.Items.Should().OnlyContain(i => i.FulfilmentState == OrderItemFulfilmentState.Refunded);
        order.Status.Should().Be(OrderStatus.Active);
    }

    /// <summary>
    /// In an order that also bought hosting, only the rejected domain line's amount is refunded,
    /// never the invoice total, rounded half away from zero to minor units.
    /// </summary>
    [Fact]
    public async Task RejectedDomainInAMixedOrderRefundsOnlyItsLine()
    {
        var plugin = new FakePaymentPlugin { RefundResult = "plugin-refund-mixed" };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, productId) = await SeedClientAndProductAsync(context);
        var (orderId, invoice) = await SeedRejectedDomainOrderAsync(context, clientId, lineAmount: 10.005m, productId);
        invoice.SetGatewaySession("bank-gateway", "gw-order-mixed-1", scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
        invoice.Settle("bank-gateway", "pre-refund-txn", DateTimeOffset.UtcNow, TransactionSource.Manual);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await BuildHandler(scope).HandleAsync(new FulfillPaidOrderCommand(orderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        plugin.LastRefundAmountMinor.Should().Be(1001);
    }

    /// <summary>
    /// A gateway-paid invoice is refunded through the hosted gateway for the rejected line's amount in the
    /// invoice currency, and the refund is on the ledger. The line was
    /// the whole invoice, so the invoice ends Refunded.
    /// </summary>
    [Fact]
    public async Task RejectedDomainOnAGatewayPaymentRefundsTheLineAtTheHostedGateway()
    {
        var plugin = new FakePaymentPlugin { RefundResult = "re_card_1" };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, _) = await SeedClientAndProductAsync(context);
        var (orderId, invoice) = await SeedRejectedDomainOrderAsync(context, clientId, lineAmount: 25m);
        invoice.SetGatewaySession("bank-gateway", "gw-order-card-1", scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
        invoice.Settle("bank-gateway", "ch_card_1", DateTimeOffset.UtcNow, TransactionSource.Manual);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await BuildHandler(scope).HandleAsync(new FulfillPaidOrderCommand(orderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        plugin.LastRefundGatewayOrderId.Should().Be("gw-order-card-1");
        plugin.LastRefundAmountMinor.Should().Be(2500);
        var ledger = await context.Transactions.AsNoTracking()
            .Where(t => t.InvoiceId == invoice.Id && t.AmountOut > 0).ToListAsync(CancellationToken.None);
        ledger.Should().ContainSingle(t => t.AmountOut == 25m && t.TransactionId == "re_card_1");
        (await StatusOfAsync(context, invoice.Id)).Should().Be(InvoiceStatus.Refunded);
    }

    /// <summary>
    /// The automatic refund of a rejected domain in a mixed gateway-paid order goes through the same refund
    /// use case as the admin action: the hosted gateway gets the line's amount only, the refund's ledger row is written, and the invoice is left
    /// PartiallyRefunded because the hosting line stays paid.
    /// </summary>
    [Fact]
    public async Task RejectedDomainInAMixedGatewayOrderLeavesTheInvoicePartiallyRefunded()
    {
        var plugin = new FakePaymentPlugin { RefundResult = "re_mixed_card" };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, productId) = await SeedClientAndProductAsync(context);
        var (orderId, invoice) = await SeedRejectedDomainOrderAsync(context, clientId, lineAmount: 10m, productId);
        invoice.SetGatewaySession("bank-gateway", "gw-order-mixed-card", scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
        invoice.Settle("bank-gateway", "ch_mixed_card", DateTimeOffset.UtcNow, TransactionSource.Manual);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await BuildHandler(scope).HandleAsync(new FulfillPaidOrderCommand(orderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        plugin.LastRefundAmountMinor.Should().Be(1000);
        var ledger = await context.Transactions.AsNoTracking()
            .Where(t => t.InvoiceId == invoice.Id && t.AmountOut > 0).ToListAsync(CancellationToken.None);
        ledger.Should().ContainSingle(t => t.AmountOut == 10m && t.TransactionId == "re_mixed_card");
        (await StatusOfAsync(context, invoice.Id)).Should().Be(InvoiceStatus.PartiallyRefunded);
    }

    /// <summary>
    /// A refund that gave the credit share back but could not return the paid share is not a refunded line: the line is left
    /// partly refunded, closed to retries, for staff to finish by hand, and the order stays incomplete.
    /// </summary>
    [Fact]
    public async Task RefundThatReturnsOnlyTheCreditShareLeavesTheLineForManualAction()
    {
        var plugin = new FakePaymentPlugin();
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, _) = await SeedClientAndProductAsync(context);
        var (orderId, invoice) = await SeedRejectedDomainOrderAsync(context, clientId, lineAmount: 25m);
        await context.InvoiceItems.Where(i => i.InvoiceId == invoice.Id).LoadAsync(CancellationToken.None);
        invoice.ApplyCredit(10m);
        invoice.SetGatewaySession("bank-gateway", "gw-order-partial-1", scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
        invoice.Settle("bank-gateway", "ch_partial_1", DateTimeOffset.UtcNow, TransactionSource.Manual);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await BuildHandler(scope).HandleAsync(new FulfillPaidOrderCommand(orderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await CreditBalanceOfAsync(host, clientId)).Should().Be(10m);
        var order = await OrdersTestReads.ReadOrderWithItemsAsync(host.Services, orderId);
        order.Items.Should().ContainSingle().Which.FulfilmentState.Should().Be(OrderItemFulfilmentState.PartiallyRefunded);
        order.Status.Should().Be(OrderStatus.Incomplete);
    }

    /// <summary>
    /// A refund that returned nothing at all (the gateway refused it and no credit was involved) does not close the line: it
    /// goes back to failed so staff can retry it, and the order stays incomplete.
    /// </summary>
    [Fact]
    public async Task RefundThatReturnsNothingLeavesTheLineFailedAndRetryable()
    {
        var plugin = new FakePaymentPlugin();
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, _) = await SeedClientAndProductAsync(context);
        var (orderId, invoice) = await SeedRejectedDomainOrderAsync(context, clientId, lineAmount: 25m);
        invoice.SetGatewaySession("bank-gateway", "gw-order-nothing-1", scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
        invoice.Settle("bank-gateway", "ch_nothing_1", DateTimeOffset.UtcNow, TransactionSource.Manual);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await BuildHandler(scope).HandleAsync(new FulfillPaidOrderCommand(orderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        (await CreditBalanceOfAsync(host, clientId)).Should().Be(0m);
        var order = await OrdersTestReads.ReadOrderWithItemsAsync(host.Services, orderId);
        var line = order.Items.Should().ContainSingle().Which;
        line.FulfilmentState.Should().Be(OrderItemFulfilmentState.Failed);
        line.FulfilmentErrorCode.Should().Be(HostpanelErrorCodes.DOMAIN_ALREADY_REGISTERED);
        order.Status.Should().Be(OrderStatus.Incomplete);
        order.CanRetryLine(line.Id, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10)).Should().BeTrue();
    }

    /// <summary>
    /// A registrar that times out leaves the registration's outcome unknown, so the line is not refunded: the client could
    /// otherwise keep the domain and the money. The line stays failed with the code for staff to retry or refund by hand.
    /// </summary>
    [Fact]
    public async Task ADomainLineWhoseOutcomeIsUnknownIsNotRefunded()
    {
        var plugin = new FakePaymentPlugin { RefundResult = "re_never_unknown" };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver, configure: s => s.AddScoped<IRegistrarProvider, TimingOutRegistrar>());
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, _) = await SeedClientAndProductAsync(context);
        var (orderId, invoice) = await SeedRejectedDomainOrderAsync(context, clientId, lineAmount: 25m);
        invoice.SetGatewaySession("bank-gateway", "gw-order-unknown-1", scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
        invoice.Settle("bank-gateway", "ch_unknown_1", DateTimeOffset.UtcNow, TransactionSource.Manual);
        await context.SaveChangesAsync(CancellationToken.None);
        var domainName = (await OrdersTestReads.ReadOrderWithItemsAsync(host.Services, orderId)).Items.Single().Domain!;
        await OrdersTestReads.ReleaseDomainAsync(host.Services, domainName);

        var result = await BuildHandler(scope).HandleAsync(new FulfillPaidOrderCommand(orderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        plugin.LastRefundGatewayOrderId.Should().BeNull();
        var order = await OrdersTestReads.ReadOrderWithItemsAsync(host.Services, orderId);
        var line = order.Items.Should().ContainSingle().Which;
        line.FulfilmentState.Should().Be(OrderItemFulfilmentState.Failed);
        line.FulfilmentErrorCode.Should().Be(HostpanelErrorCodes.HOSTPANEL_UNEXPECTED);
    }

    /// <summary>
    /// While a failed line's refund is under way a retry cannot take the line: it is refused, the domain is not registered,
    /// and once the refund returns the line is Refunded, so the client is never both refunded and delivered.
    /// </summary>
    [Fact]
    public async Task ARetryCannotTakeALineWhileItsRefundIsUnderWay()
    {
        var gateway = new GatedRefundPlugin();
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", gateway);
        var host = BuildHost(resolver);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, _) = await SeedClientAndProductAsync(context);
        var (orderId, invoice) = await SeedRejectedDomainOrderAsync(context, clientId, lineAmount: 25m);
        invoice.SetGatewaySession("bank-gateway", "gw-order-race-1", scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
        invoice.Settle("bank-gateway", "ch_race_1", DateTimeOffset.UtcNow, TransactionSource.Manual);
        await context.SaveChangesAsync(CancellationToken.None);
        var line = (await OrdersTestReads.ReadOrderWithItemsAsync(host.Services, orderId)).Items.Single();
        var handler = BuildHandler(scope);

        var fulfilment = Task.Run(() => handler.HandleAsync(new FulfillPaidOrderCommand(orderId), CancellationToken.None));
        await gateway.RefundStarted.Task.WaitAsync(TimeSpan.FromSeconds(60));
        await OrdersTestReads.ReleaseDomainAsync(host.Services, line.Domain!);
        var admin = await OrdersTestReads.AdminOfAsync(host, factory);
        var retry = await admin.PostAsync($"/api/orders/{orderId}/items/{line.Id}/retry", content: null);

        retry.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await OrdersTestSeeds.AssertCodeAsync(retry, HostpanelErrorCodes.ORDER_LINE_NOT_RETRYABLE);
        (await OrdersTestReads.CountDomainsAsync(host.Services, line.Domain!, clientId)).Should().Be(0);
        (await OrdersTestReads.ReadItemsAsync(host.Services, orderId)).Single().FulfilmentState.Should().Be(OrderItemFulfilmentState.Refunding);

        gateway.Release.SetResult();
        (await fulfilment).IsSuccess.Should().BeTrue();
        var order = await OrdersTestReads.ReadOrderWithItemsAsync(host.Services, orderId);
        order.Items.Single().FulfilmentState.Should().Be(OrderItemFulfilmentState.Refunded);
        order.Status.Should().Be(OrderStatus.Active);
    }

    /// <summary>Reads an invoice's status as the database now holds it, past the scope's tracked copy.</summary>
    /// <param name="context">The context to read through.</param>
    /// <param name="invoiceId">The invoice.</param>
    /// <returns>The stored status.</returns>
    private static Task<InvoiceStatus> StatusOfAsync(IHostpanelDbContext context, int invoiceId) =>
        context.Invoices.AsNoTracking()
            .Where(i => i.Id == invoiceId)
            .Select(i => i.Status)
            .SingleAsync(CancellationToken.None);

    /// <summary>
    /// On a taxed invoice the rejected line is refunded with its tax: the refund is the line's share
    /// of the invoice total, not its pre-tax price.
    /// </summary>
    [Fact]
    public async Task RejectedDomainOnATaxedInvoiceRefundsTheLineWithItsTax()
    {
        var plugin = new FakePaymentPlugin { RefundResult = "re_taxed_1" };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, _) = await SeedClientAndProductAsync(context);
        var (orderId, invoice) = await SeedRejectedDomainOrderAsync(context, clientId, lineAmount: 25m);
        await context.InvoiceItems.Where(i => i.InvoiceId == invoice.Id).LoadAsync(CancellationToken.None);
        invoice.UpdateOptions(invoice.InvoiceDate, invoice.DueDate, invoice.PaymentMethod, taxRate: 10m);
        invoice.SetGatewaySession("bank-gateway", "gw-order-taxed-1", scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
        invoice.Settle("bank-gateway", "ch_taxed_1", DateTimeOffset.UtcNow, TransactionSource.Manual);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await BuildHandler(scope).HandleAsync(new FulfillPaidOrderCommand(orderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        invoice.Total.Should().Be(27.5m);
        plugin.LastRefundAmountMinor.Should().Be(2750);
    }

    /// <summary>
    /// With the real default registrar provider and no registrar credentials, a paid registration is refused,
    /// not faked: no domain is recorded and the payment is refunded automatically.
    /// </summary>
    [Fact]
    public async Task UnconfiguredRegistrarRefundsAndRecordsNoDomain()
    {
        var plugin = new FakePaymentPlugin { RefundResult = "plugin-refund-2" };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver, useRealRegistrar: true);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, _) = await SeedClientAndProductAsync(context);

        var domainName = $"{Guid.NewGuid():N}"[..12] + ".am";
        var domainProductId = await DomainsTestSeeds.SeedHostingProductAsync(context);
        var orderId = await SeedPendingOrderAsync(
            context, clientId,
            order => order.AddItem(
                domainProductId, domainName, BillingCycle.Annual, 25m, 25m, domain: domainName, hostname: null,
                domainAction: DomainAction.Register, years: 1));

        var order = await context.Orders.FindAsync(orderId);
        var invoice = await context.Invoices.FindAsync(order!.InvoiceId!.Value);
        invoice!.SetGatewaySession("bank-gateway", "gw-order-unconfigured-1", scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
        invoice.Settle("bank-gateway", "pre-refund-txn", DateTimeOffset.UtcNow, TransactionSource.Manual);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await BuildHandler(scope).HandleAsync(new FulfillPaidOrderCommand(orderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        plugin.LastRefundGatewayOrderId.Should().Be("gw-order-unconfigured-1");
        (await context.Domains.AnyAsync(d => d.Name == domainName, CancellationToken.None)).Should().BeFalse();
    }

    /// <summary>
    /// A rejected line on an invoice paid partly with credit gives the credit share back to the
    /// client's credit balance and only the paid share back through the hosted gateway.
    /// </summary>
    [Fact]
    public async Task RejectedDomainPaidPartlyWithCreditRefundsEachShareItsOwnWay()
    {
        var plugin = new FakePaymentPlugin { RefundResult = "re_split_1" };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, _) = await SeedClientAndProductAsync(context);
        var (orderId, invoice) = await SeedRejectedDomainOrderAsync(context, clientId, lineAmount: 25m);
        await context.InvoiceItems.Where(i => i.InvoiceId == invoice.Id).LoadAsync(CancellationToken.None);
        invoice.ApplyCredit(10m);
        invoice.SetGatewaySession("bank-gateway", "gw-order-split-1", scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
        invoice.Settle("bank-gateway", "ch_split_1", DateTimeOffset.UtcNow, TransactionSource.Manual);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await BuildHandler(scope).HandleAsync(new FulfillPaidOrderCommand(orderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        plugin.LastRefundAmountMinor.Should().Be(1500);
        (await CreditBalanceOfAsync(host, clientId)).Should().Be(10m);
    }

    /// <summary>
    /// A rejected line on an invoice paid wholly with credit goes back to the credit balance only:
    /// no gateway is asked for a refund of money it never took.
    /// </summary>
    [Fact]
    public async Task RejectedDomainPaidWhollyWithCreditRefundsToCreditOnly()
    {
        var plugin = new FakePaymentPlugin { RefundResult = "re_never" };
        var resolver = new FakePaymentPluginResolver();
        resolver.Register("bank-gateway", plugin);
        var host = BuildHost(resolver);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, _) = await SeedClientAndProductAsync(context);
        var (orderId, invoice) = await SeedRejectedDomainOrderAsync(context, clientId, lineAmount: 25m);
        await context.InvoiceItems.Where(i => i.InvoiceId == invoice.Id).LoadAsync(CancellationToken.None);
        invoice.ApplyCredit(25m);
        invoice.Settle(PaymentMethodNames.CreditBalance, "credit-paid", DateTimeOffset.UtcNow, TransactionSource.Manual);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await BuildHandler(scope).HandleAsync(new FulfillPaidOrderCommand(orderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        plugin.LastRefundGatewayOrderId.Should().BeNull();
        (await CreditBalanceOfAsync(host, clientId)).Should().Be(25m);
    }

    /// <summary>Reads a client's credit balance from a fresh scope, so the value is what was saved.</summary>
    /// <param name="host">The test host.</param>
    /// <param name="clientId">The client.</param>
    /// <returns>The saved credit balance.</returns>
    private static async Task<decimal> CreditBalanceOfAsync(WebApplicationFactory<Program> host, int clientId)
    {
        using var readScope = host.Services.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        return await readContext.Clients.AsNoTracking()
            .Where(c => c.Id == clientId)
            .Select(c => c.CreditBalance)
            .SingleAsync(CancellationToken.None);
    }
}
