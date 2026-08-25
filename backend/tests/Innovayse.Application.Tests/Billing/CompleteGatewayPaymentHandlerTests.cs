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

        Assert.Equal(GatewayCompletionState.Paid, result);
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

        Assert.Equal(GatewayCompletionState.Paid, result);
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

        Assert.Equal(GatewayCompletionState.Pending, result);
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

        Assert.Equal(GatewayCompletionState.Declined, result);
        Assert.Equal(InvoiceStatus.Unpaid, invoice.Status);
    }

    [Fact]
    public async Task HandleAsync_GatewaySaysPaid_RetainsGatewaySession()
    {
        // Critical 2 regression guard: the completion path must use the session-retaining
        // domain method so refund/reconciliation code can still identify the paying gateway.
        var invoice = CreateInvoiceWithSession();
        plugin.Setup(p => p.GetStatusAsync("gw-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentStatus(GatewayPaymentState.Paid, "ref-9", "orderStatus:2"));

        await CreateHandler().HandleAsync(new CompleteGatewayPaymentCommand(invoice.Id), CancellationToken.None);

        Assert.Equal("innovayse-inecobank", invoice.GatewayModule);
        Assert.Equal("gw-1", invoice.GatewayOrderId);
    }

    [Fact]
    public async Task HandleAsync_ConcurrentCompletionAlreadyWon_ReturnsPaidWithoutThrowing()
    {
        // Two simultaneous completes (the result page's auto-check racing the retry button, or
        // the reconciler firing as the payer returns) can both pass the status guard. The loser's
        // SaveChangesAsync should hit the xmin concurrency token; the handler must not surface
        // that as a failure once the winner already recorded the payment.
        var invoice = CreateInvoiceWithSession();
        plugin.Setup(p => p.GetStatusAsync("gw-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentStatus(GatewayPaymentState.Paid, "ref-9", "orderStatus:2"));
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("concurrency conflict"));

        var winner = Invoice.Create(clientId: 1, dueDate: DateTimeOffset.UtcNow.AddDays(14));
        winner.AddItem("Hosting", 10m, 1);
        winner.SetGatewaySession("innovayse-inecobank", "gw-1");
        winner.MarkPaidViaGateway("ref-9");
        invoiceRepo.SetupSequence(r => r.FindByIdAsync(invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice)
            .ReturnsAsync(winner);

        var result = await CreateHandler().HandleAsync(
            new CompleteGatewayPaymentCommand(invoice.Id), CancellationToken.None);

        Assert.Equal(GatewayCompletionState.Paid, result);
        orderRepo.Verify(
            r => r.FindByInvoiceIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);

        // The re-read after a concurrency conflict is only meaningful if the stale tracked
        // instance was detached first; otherwise EF identity resolution would return the same
        // (still-Paid-in-memory) instance regardless of what the database actually holds.
        uow.Verify(u => u.DetachAll(), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ConcurrentCompletionOtherWriterDidNotWin_Rethrows()
    {
        var invoice = CreateInvoiceWithSession();
        plugin.Setup(p => p.GetStatusAsync("gw-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GatewayPaymentStatus(GatewayPaymentState.Paid, "ref-9", "orderStatus:2"));
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException("concurrency conflict"));

        // The reload must be a genuinely distinct, still-Unpaid instance — reusing `invoice`
        // would reflect its in-memory MarkPaidViaGateway() mutation even though SaveChanges
        // never persisted it, masking the very race this test exercises.
        var stillUnpaidOnReload = Invoice.Create(clientId: 1, dueDate: DateTimeOffset.UtcNow.AddDays(14));
        stillUnpaidOnReload.AddItem("Hosting", 10m, 1);
        stillUnpaidOnReload.SetGatewaySession("innovayse-inecobank", "gw-1");
        invoiceRepo.SetupSequence(r => r.FindByIdAsync(invoice.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoice)
            .ReturnsAsync(stillUnpaidOnReload); // some other, unrelated conflict — not a race we already won

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => CreateHandler().HandleAsync(
            new CompleteGatewayPaymentCommand(invoice.Id), CancellationToken.None));

        // Same as above: the rethrow is only correct if the re-read actually consulted the
        // database (via a detach) instead of returning the still-tracked, in-memory-mutated
        // `invoice` instance.
        uow.Verify(u => u.DetachAll(), Times.Once);
    }
}
