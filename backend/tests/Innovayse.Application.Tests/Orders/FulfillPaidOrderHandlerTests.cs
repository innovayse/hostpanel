namespace Innovayse.Application.Tests.Orders;

using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Domains.Commands.RegisterDomain;
using Innovayse.Application.Orders.Commands.FulfillPaidOrder;
using Innovayse.Application.Services.Commands.OrderService;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Domains.Interfaces;
using Innovayse.Domain.Orders;
using Innovayse.Domain.Orders.Interfaces;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wolverine;
using Xunit;

/// <summary>Tests for <see cref="FulfillPaidOrderHandler"/> idempotency, dispatch, and refund routing.</summary>
public class FulfillPaidOrderHandlerTests
{
    private readonly Mock<IOrderRepository> orderRepo = new();
    private readonly Mock<IInvoiceRepository> invoiceRepo = new();
    private readonly Mock<IDomainRepository> domainRepo = new();
    private readonly Mock<IStripeService> stripeService = new();
    private readonly Mock<IPaymentPluginResolver> pluginResolver = new();
    private readonly Mock<IUnitOfWork> uow = new();
    private readonly Mock<IMessageBus> bus = new();

    private FulfillPaidOrderHandler CreateHandler() => new(
        orderRepo.Object, invoiceRepo.Object, stripeService.Object, pluginResolver.Object,
        domainRepo.Object, uow.Object, bus.Object, NullLogger<FulfillPaidOrderHandler>.Instance);

    /// <summary>Builds a Paid invoice with a positive total, linked to the given gateway module/order id.</summary>
    private static Invoice MakePaidInvoiceWithGatewaySession(string module, string gatewayOrderId)
    {
        var invoice = Invoice.Create(clientId: 5, dueDate: DateTimeOffset.UtcNow.AddDays(7));
        invoice.AddItem("Domain registration", 25m, 1);
        invoice.SetGatewaySession(module, gatewayOrderId);
        invoice.MarkPaid("irrelevant-pre-gateway-txn-id");
        return invoice;
    }

    [Fact]
    public async Task HandleAsync_PendingOrderWithServiceItem_AcceptsAndDispatchesService()
    {
        var order = Order.Create(orderNumber: "ORD-1", clientId: 5, paymentMethod: "innovayse-inecobank", ipAddress: null);
        order.AddItem(
            productId: 3,
            productName: "Hosting Plan",
            billingCycle: "monthly",
            firstPaymentAmount: 10m,
            recurringAmount: 10m,
            domain: null,
            hostname: null);
        order.LinkInvoice(10);
        orderRepo.Setup(r => r.FindByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        invoiceRepo.Setup(r => r.FindByIdAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Invoice.Create(clientId: 5, dueDate: DateTimeOffset.UtcNow.AddDays(7)));
        bus.Setup(b => b.InvokeAsync<int>(It.IsAny<OrderServiceCommand>(), It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(77);

        await CreateHandler().HandleAsync(new FulfillPaidOrderCommand(1), CancellationToken.None);

        Assert.Equal(OrderStatus.Active, order.Status);
        bus.Verify(
            b => b.InvokeAsync<int>(It.IsAny<OrderServiceCommand>(), It.IsAny<CancellationToken>(), null),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_AlreadyAcceptedOrder_IsNoOp()
    {
        var order = Order.Create(orderNumber: "ORD-1", clientId: 5, paymentMethod: "stripe", ipAddress: null);
        order.AddItem(
            productId: 3,
            productName: "Hosting Plan",
            billingCycle: "monthly",
            firstPaymentAmount: 10m,
            recurringAmount: 10m,
            domain: null,
            hostname: null);
        order.Accept();
        orderRepo.Setup(r => r.FindByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        await CreateHandler().HandleAsync(new FulfillPaidOrderCommand(1), CancellationToken.None);

        bus.Verify(
            b => b.InvokeAsync<int>(It.IsAny<object>(), It.IsAny<CancellationToken>(), null),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_RegistrarRejectsDomain_PluginPaidInvoice_RefundsThroughPluginAndRecordsIt()
    {
        var order = Order.Create(orderNumber: "ORD-2", clientId: 5, paymentMethod: "innovayse-inecobank", ipAddress: null);
        order.AddItem(
            productId: 9,
            productName: "example.com",
            billingCycle: "annual",
            firstPaymentAmount: 25m,
            recurringAmount: 25m,
            domain: "example.com",
            hostname: null,
            domainAction: "register",
            years: 1);
        order.LinkInvoice(10);

        var invoice = MakePaidInvoiceWithGatewaySession("innovayse-inecobank", "gw-order-1");

        orderRepo.Setup(r => r.FindByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        invoiceRepo.Setup(r => r.FindByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(invoice);
        bus.Setup(b => b.InvokeAsync<int>(It.IsAny<RegisterDomainCommand>(), It.IsAny<CancellationToken>(), null))
            .ThrowsAsync(new InvalidOperationException("Registrar rejected: domain already registered."));

        var plugin = new Mock<IPaymentPlugin>();
        plugin.Setup(p => p.RefundAsync("gw-order-1", 2500L, It.IsAny<CancellationToken>()))
            .ReturnsAsync("plugin-refund-99");
        pluginResolver.Setup(r => r.ResolveAsync("innovayse-inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);

        await CreateHandler().HandleAsync(new FulfillPaidOrderCommand(2), CancellationToken.None);

        plugin.Verify(p => p.RefundAsync("gw-order-1", 2500L, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(InvoiceStatus.Refunded, invoice.Status);
        Assert.Contains(
            invoice.Transactions,
            t => t.Type == InvoiceTransactionType.Refund
                && t.TransactionId == "plugin-refund-99"
                && t.Gateway == "innovayse-inecobank");
        stripeService.Verify(s => s.RefundAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_RegistrarRejectsDomain_ResolverReturnsNull_LogsCriticalWithoutThrowing()
    {
        var order = Order.Create(orderNumber: "ORD-3", clientId: 5, paymentMethod: "innovayse-inecobank", ipAddress: null);
        order.AddItem(
            productId: 9,
            productName: "example.com",
            billingCycle: "annual",
            firstPaymentAmount: 25m,
            recurringAmount: 25m,
            domain: "example.com",
            hostname: null,
            domainAction: "register",
            years: 1);
        order.LinkInvoice(10);

        var invoice = MakePaidInvoiceWithGatewaySession("innovayse-inecobank", "gw-order-2");

        orderRepo.Setup(r => r.FindByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        invoiceRepo.Setup(r => r.FindByIdAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(invoice);
        bus.Setup(b => b.InvokeAsync<int>(It.IsAny<RegisterDomainCommand>(), It.IsAny<CancellationToken>(), null))
            .ThrowsAsync(new InvalidOperationException("Registrar rejected: invalid TLD."));
        pluginResolver.Setup(r => r.ResolveAsync("innovayse-inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync((IPaymentPlugin?)null);

        // Should not throw: the null-resolver failure is caught, logged critically, and swallowed —
        // same contract as any other failed auto-refund attempt.
        var exception = await Record.ExceptionAsync(
            () => CreateHandler().HandleAsync(new FulfillPaidOrderCommand(3), CancellationToken.None));

        Assert.Null(exception);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Empty(invoice.Transactions);
        Assert.DoesNotContain(bus.Invocations, i => i.Method.Name == nameof(IMessageBus.PublishAsync));
    }
}
