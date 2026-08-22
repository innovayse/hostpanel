namespace Innovayse.Application.Tests.Orders;

using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Orders.Commands.FulfillPaidOrder;
using Innovayse.Application.Services.Commands.OrderService;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Domains.Interfaces;
using Innovayse.Domain.Orders;
using Innovayse.Domain.Orders.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wolverine;
using Xunit;

/// <summary>Tests for <see cref="FulfillPaidOrderHandler"/> idempotency and dispatch.</summary>
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
        orderRepo.Setup(r => r.FindByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(order);
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
}
