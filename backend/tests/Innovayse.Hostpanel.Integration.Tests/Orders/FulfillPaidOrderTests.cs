using Innovayse.Hostpanel.Application.Billing.Common;
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
/// rejected domain line, through a hosted gateway plugin or the card gateway.
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
    /// Builds a host whose refund command reaches <paramref name="pluginResolver"/> and
    /// <paramref name="cardGateway"/> instead of a real gateway.
    /// </summary>
    /// <param name="pluginResolver">The fake plugin resolver the refund resolves hosted gateways through.</param>
    /// <param name="cardGateway">The fake card gateway the refund calls for a card payment.</param>
    /// <param name="useRealRegistrar">Whether domain registration goes to the real, unconfigured registrar provider.</param>
    /// <returns>The test host.</returns>
    private WebApplicationFactory<Program> BuildHost(
        FakePaymentPluginResolver pluginResolver, FakeCardPaymentGateway cardGateway, bool useRealRegistrar = false) =>
        factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.AddSingleton<IPaymentPluginResolver>(pluginResolver);
            s.AddSingleton<ICardPaymentGateway>(cardGateway);
            if (useRealRegistrar)
            {
                s.AddScoped<IRegistrarProvider, NameAmRegistrarProvider>();
            }
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
        var reloaded = await context.Orders.FindAsync(orderId);
        reloaded!.Status.Should().Be(OrderStatus.Active);

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
        var host = BuildHost(resolver, new FakeCardPaymentGateway { IsConfigured = true });
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, _) = await SeedClientAndProductAsync(context);
        var (orderId, invoice) = await SeedRejectedDomainOrderAsync(context, clientId, lineAmount: 25m);
        invoice.SetGatewaySession("bank-gateway", "gw-order-refund-1", scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
        invoice.Settle("bank-gateway", "pre-refund-txn", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await BuildHandler(scope).HandleAsync(new FulfillPaidOrderCommand(orderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        plugin.LastRefundGatewayOrderId.Should().Be("gw-order-refund-1");
        plugin.LastRefundAmountMinor.Should().Be(2500);
        var ledger = await context.Transactions.AsNoTracking()
            .Where(t => t.InvoiceId == invoice.Id && t.AmountOut > 0).ToListAsync(CancellationToken.None);
        ledger.Should().ContainSingle(t => t.AmountOut == 25m && t.TransactionId == "plugin-refund-1");
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
        var host = BuildHost(resolver, new FakeCardPaymentGateway { IsConfigured = true });
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, productId) = await SeedClientAndProductAsync(context);
        var (orderId, invoice) = await SeedRejectedDomainOrderAsync(context, clientId, lineAmount: 10.005m, productId);
        invoice.SetGatewaySession("bank-gateway", "gw-order-mixed-1", scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
        invoice.Settle("bank-gateway", "pre-refund-txn", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await BuildHandler(scope).HandleAsync(new FulfillPaidOrderCommand(orderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        plugin.LastRefundAmountMinor.Should().Be(1001);
    }

    /// <summary>
    /// A card-paid invoice is refunded at the card gateway for the rejected line's amount in the
    /// invoice currency, against the original charge, and the refund is on the ledger. The line was
    /// the whole invoice, so the invoice ends Refunded.
    /// </summary>
    [Fact]
    public async Task RejectedDomainOnACardPaymentRefundsTheLineAtTheCardGateway()
    {
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true, RefundId = "re_card_1" };
        var host = BuildHost(new FakePaymentPluginResolver(), cardGateway);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, _) = await SeedClientAndProductAsync(context);
        var (orderId, invoice) = await SeedRejectedDomainOrderAsync(context, clientId, lineAmount: 25m);
        var payment = invoice.Settle(PaymentMethodNames.Card, "ch_card_1", DateTimeOffset.UtcNow);
        context.Transactions.Add(payment!);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await BuildHandler(scope).HandleAsync(new FulfillPaidOrderCommand(orderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        cardGateway.RefundInstructions.Should().ContainSingle()
            .Which.Should().Be(new CardRefundInstruction("ch_card_1", 25m, "USD", $"refund-{invoice.Id}-ch_card_1-0"));
        var ledger = await context.Transactions.AsNoTracking()
            .Where(t => t.InvoiceId == invoice.Id && t.AmountOut > 0).ToListAsync(CancellationToken.None);
        ledger.Should().ContainSingle(t => t.AmountOut == 25m && t.TransactionId == "re_card_1");
        (await StatusOfAsync(context, invoice.Id)).Should().Be(InvoiceStatus.Refunded);
    }

    /// <summary>
    /// The automatic refund of a rejected domain in a mixed card-paid order goes through the same refund
    /// use case as the admin action: the card gateway gets the original charge's reference for the
    /// line's amount only, the refund's ledger row is written, and the invoice is left
    /// PartiallyRefunded because the hosting line stays paid.
    /// </summary>
    [Fact]
    public async Task RejectedDomainInAMixedCardOrderLeavesTheInvoicePartiallyRefunded()
    {
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true, RefundId = "re_mixed_card" };
        var host = BuildHost(new FakePaymentPluginResolver(), cardGateway);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, productId) = await SeedClientAndProductAsync(context);
        var (orderId, invoice) = await SeedRejectedDomainOrderAsync(context, clientId, lineAmount: 10m, productId);
        var payment = invoice.Settle(PaymentMethodNames.Card, "ch_mixed_card", DateTimeOffset.UtcNow);
        context.Transactions.Add(payment!);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await BuildHandler(scope).HandleAsync(new FulfillPaidOrderCommand(orderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        cardGateway.RefundInstructions.Should().ContainSingle()
            .Which.Should().Be(new CardRefundInstruction("ch_mixed_card", 10m, "USD", $"refund-{invoice.Id}-ch_mixed_card-0"));
        var ledger = await context.Transactions.AsNoTracking()
            .Where(t => t.InvoiceId == invoice.Id && t.AmountOut > 0).ToListAsync(CancellationToken.None);
        ledger.Should().ContainSingle(t => t.AmountOut == 10m && t.TransactionId == "re_mixed_card");
        (await StatusOfAsync(context, invoice.Id)).Should().Be(InvoiceStatus.PartiallyRefunded);
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
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true, RefundId = "re_taxed_1" };
        var host = BuildHost(new FakePaymentPluginResolver(), cardGateway);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, _) = await SeedClientAndProductAsync(context);
        var (orderId, invoice) = await SeedRejectedDomainOrderAsync(context, clientId, lineAmount: 25m);
        await context.InvoiceItems.Where(i => i.InvoiceId == invoice.Id).LoadAsync(CancellationToken.None);
        invoice.UpdateOptions(invoice.InvoiceDate, invoice.DueDate, invoice.PaymentMethod, taxRate: 10m);
        var payment = invoice.Settle(PaymentMethodNames.Card, "ch_taxed_1", DateTimeOffset.UtcNow);
        context.Transactions.Add(payment!);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await BuildHandler(scope).HandleAsync(new FulfillPaidOrderCommand(orderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        invoice.Total.Should().Be(27.5m);
        cardGateway.RefundInstructions.Should().ContainSingle()
            .Which.Should().Be(new CardRefundInstruction("ch_taxed_1", 27.5m, "USD", $"refund-{invoice.Id}-ch_taxed_1-0"));
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
        var host = BuildHost(resolver, new FakeCardPaymentGateway { IsConfigured = true }, useRealRegistrar: true);
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
        invoice.Settle("bank-gateway", "pre-refund-txn", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await BuildHandler(scope).HandleAsync(new FulfillPaidOrderCommand(orderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        plugin.LastRefundGatewayOrderId.Should().Be("gw-order-unconfigured-1");
        (await context.Domains.AnyAsync(d => d.Name == domainName, CancellationToken.None)).Should().BeFalse();
    }

    /// <summary>
    /// A rejected line on an invoice paid partly with credit gives the credit share back to the
    /// client's credit balance and only the paid share back through the card gateway.
    /// </summary>
    [Fact]
    public async Task RejectedDomainPaidPartlyWithCreditRefundsEachShareItsOwnWay()
    {
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true, RefundId = "re_split_1" };
        var host = BuildHost(new FakePaymentPluginResolver(), cardGateway);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, _) = await SeedClientAndProductAsync(context);
        var (orderId, invoice) = await SeedRejectedDomainOrderAsync(context, clientId, lineAmount: 25m);
        await context.InvoiceItems.Where(i => i.InvoiceId == invoice.Id).LoadAsync(CancellationToken.None);
        invoice.ApplyCredit(10m);
        var payment = invoice.Settle(PaymentMethodNames.Card, "ch_split_1", DateTimeOffset.UtcNow);
        context.Transactions.Add(payment!);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await BuildHandler(scope).HandleAsync(new FulfillPaidOrderCommand(orderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        cardGateway.RefundInstructions.Should().ContainSingle()
            .Which.Should().Be(new CardRefundInstruction("ch_split_1", 15m, "USD", $"refund-{invoice.Id}-ch_split_1-0"));
        (await CreditBalanceOfAsync(host, clientId)).Should().Be(10m);
    }

    /// <summary>
    /// A rejected line on an invoice paid wholly with credit goes back to the credit balance only:
    /// no gateway is asked for a refund of money it never took.
    /// </summary>
    [Fact]
    public async Task RejectedDomainPaidWhollyWithCreditRefundsToCreditOnly()
    {
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true, RefundId = "re_never" };
        var host = BuildHost(new FakePaymentPluginResolver(), cardGateway);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, _) = await SeedClientAndProductAsync(context);
        var (orderId, invoice) = await SeedRejectedDomainOrderAsync(context, clientId, lineAmount: 25m);
        await context.InvoiceItems.Where(i => i.InvoiceId == invoice.Id).LoadAsync(CancellationToken.None);
        invoice.ApplyCredit(25m);
        invoice.Settle(PaymentMethodNames.CreditBalance, "credit-paid", DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(CancellationToken.None);

        var result = await BuildHandler(scope).HandleAsync(new FulfillPaidOrderCommand(orderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        cardGateway.RefundInstructions.Should().BeEmpty();
        (await CreditBalanceOfAsync(host, clientId)).Should().Be(25m);
    }

    /// <summary>
    /// When the card gateway fails part way through the line's refund, the part it returned is on
    /// the ledger and the critical log asks for a manual refund of only what is left.
    /// </summary>
    [Fact]
    public async Task RejectedDomainWithAPartlyFailedRefundLogsOnlyTheRemainingAmount()
    {
        var cardGateway = new FakeCardPaymentGateway { IsConfigured = true, RefundId = "re_part_1", ThrowsOnRefundCall = 2 };
        var host = BuildHost(new FakePaymentPluginResolver(), cardGateway);
        using var scope = host.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var (clientId, _) = await SeedClientAndProductAsync(context);
        var (orderId, invoice) = await SeedRejectedDomainOrderAsync(context, clientId, lineAmount: 25m);
        await context.InvoiceItems.Where(i => i.InvoiceId == invoice.Id).LoadAsync(CancellationToken.None);
        var now = DateTimeOffset.UtcNow;
        context.Transactions.Add(invoice.AddPayment(now.AddMinutes(-5), PaymentMethodNames.Card, "ch_part_old", 10m, 0m, null));
        context.Transactions.Add(invoice.Settle(PaymentMethodNames.Card, "ch_part_new", now)!);
        await context.SaveChangesAsync(CancellationToken.None);
        var logger = new CapturingLogger<FulfillPaidOrderHandler>();

        var result = await BuildHandler(scope, logger).HandleAsync(new FulfillPaidOrderCommand(orderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        cardGateway.RefundInstructions.Should().Equal(
            new CardRefundInstruction("ch_part_new", 15m, "USD", $"refund-{invoice.Id}-ch_part_new-0"),
            new CardRefundInstruction("ch_part_old", 10m, "USD", $"refund-{invoice.Id}-ch_part_old-0"));
        var ledger = await context.Transactions.AsNoTracking()
            .Where(t => t.InvoiceId == invoice.Id && t.AmountOut > 0).ToListAsync(CancellationToken.None);
        ledger.Should().ContainSingle(t => t.AmountOut == 15m && t.TransactionId == "re_part_1");
        logger.Messages.Should().ContainSingle(m => m.StartsWith("REFUND FAILED", StringComparison.Ordinal))
            .Which.Should().Contain("remaining 10");
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
