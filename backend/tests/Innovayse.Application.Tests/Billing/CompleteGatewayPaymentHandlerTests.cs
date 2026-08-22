namespace Innovayse.Application.Tests.Billing;

using Innovayse.Application.Billing.Commands.CompleteGatewayPayment;
using Innovayse.Application.Billing.Interfaces;
using Innovayse.Application.Common;
using Innovayse.Application.Orders.Commands.FulfillPaidOrder;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Innovayse.Domain.Orders.Interfaces;
using Innovayse.SDK.Plugins;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wolverine;
using Xunit;

/// <summary>Tests for <see cref="CompleteGatewayPaymentHandler"/>.</summary>
public class CompleteGatewayPaymentHandlerTests
{
    private readonly Mock<IInvoiceRepository> invoiceRepo = new();
    private readonly Mock<IOrderRepository> orderRepo = new();
    private readonly Mock<IPaymentPluginResolver> resolver = new();
    private readonly Mock<IPaymentPlugin> plugin = new();
    private readonly Mock<IUnitOfWork> uow = new();
    private readonly Mock<IMessageBus> bus = new();

    private CompleteGatewayPaymentHandler CreateHandler() => new(
        invoiceRepo.Object, orderRepo.Object, resolver.Object, uow.Object, bus.Object,
        NullLogger<CompleteGatewayPaymentHandler>.Instance);

    private Invoice CreateInvoiceWithSession()
    {
        var invoice = Invoice.Create(clientId: 1, dueDate: DateTimeOffset.UtcNow.AddDays(14));
        invoice.AddItem("Hosting", 10m, 1);
        invoice.SetGatewaySession("innovayse-inecobank", "gw-1");
        invoiceRepo.Setup(r => r.FindByIdAsync(invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice);
        resolver.Setup(r => r.ResolveAsync("innovayse-inecobank", It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin.Object);
        orderRepo.Setup(r => r.FindByInvoiceIdAsync(invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Innovayse.Domain.Orders.Order?)null);
        return invoice;
    }

    [Fact]
    public async Task HandleAsync_GatewaySaysPaid_MarksPaidWithGatewayTransaction()
    {
        var invoice = CreateInvoiceWithSession();
        plugin.Setup(p => p.GetStatusAsync("gw-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentStatus(GatewayPaymentState.Paid, "ref-9", "orderStatus:2"));

        var result = await CreateHandler().HandleAsync(
            new CompleteGatewayPaymentCommand(invoice.Id), CancellationToken.None);

        Assert.Equal("paid", result);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal("ref-9", invoice.GatewayTransactionId);
    }

    [Fact]
    public async Task HandleAsync_AlreadyPaid_IsIdempotent_NoGatewayCall()
    {
        var invoice = CreateInvoiceWithSession();
        invoice.MarkPaid("earlier-txn");

        var result = await CreateHandler().HandleAsync(
            new CompleteGatewayPaymentCommand(invoice.Id), CancellationToken.None);

        Assert.Equal("paid", result);
        plugin.Verify(
            p => p.GetStatusAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Pending_LeavesInvoiceUnpaid()
    {
        var invoice = CreateInvoiceWithSession();
        plugin.Setup(p => p.GetStatusAsync("gw-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentStatus(GatewayPaymentState.Pending, null, "orderStatus:0"));

        var result = await CreateHandler().HandleAsync(
            new CompleteGatewayPaymentCommand(invoice.Id), CancellationToken.None);

        Assert.Equal("pending", result);
        Assert.Equal(InvoiceStatus.Unpaid, invoice.Status);
    }

    [Fact]
    public async Task HandleAsync_Declined_LeavesInvoiceUnpaid()
    {
        var invoice = CreateInvoiceWithSession();
        plugin.Setup(p => p.GetStatusAsync("gw-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentStatus(GatewayPaymentState.Declined, null, "orderStatus:6"));

        var result = await CreateHandler().HandleAsync(
            new CompleteGatewayPaymentCommand(invoice.Id), CancellationToken.None);

        Assert.Equal("declined", result);
        Assert.Equal(InvoiceStatus.Unpaid, invoice.Status);
    }
}
