namespace Innovayse.Application.Tests.Billing;

using Innovayse.Application.Billing.Commands.CompleteGatewayPayment;
using Innovayse.Application.Billing.Commands.ReconcileGatewayPaymentsCron;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wolverine;
using Xunit;

/// <summary>Tests for <see cref="ReconcileGatewayPaymentsCronHandler"/>.</summary>
public class ReconcileGatewayPaymentsCronHandlerTests
{
    [Fact]
    public async Task HandleAsync_CompletesEachPendingInvoice_AndReschedules()
    {
        var invoiceRepo = new Mock<IInvoiceRepository>();
        var bus = new Mock<IMessageBus>();
        var a = Invoice.Create(1, DateTimeOffset.UtcNow.AddDays(7));
        a.SetGatewaySession("innovayse-inecobank", "gw-a");
        var b = Invoice.Create(2, DateTimeOffset.UtcNow.AddDays(7));
        b.SetGatewaySession("innovayse-inecobank", "gw-b");
        invoiceRepo.Setup(r => r.ListPendingGatewayPaymentsAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([a, b]);
        bus.Setup(x => x.InvokeAsync<string>(It.IsAny<CompleteGatewayPaymentCommand>(), It.IsAny<CancellationToken>(), null))
            .ReturnsAsync("pending");

        // ScheduleAsync() is a static Wolverine extension that delegates to
        // IMessageBus.PublishAsync(message, DeliveryOptions) under the hood — that is
        // the real interface member Moq can intercept and verify against.
        bus.Setup(x => x.PublishAsync(It.IsAny<ReconcileGatewayPaymentsCronCommand>(), It.IsAny<DeliveryOptions>()))
            .Returns(ValueTask.CompletedTask);

        var handler = new ReconcileGatewayPaymentsCronHandler(
            invoiceRepo.Object, bus.Object, NullLogger<ReconcileGatewayPaymentsCronHandler>.Instance);
        await handler.HandleAsync(new ReconcileGatewayPaymentsCronCommand(), CancellationToken.None);

        bus.Verify(
            x => x.InvokeAsync<string>(It.IsAny<CompleteGatewayPaymentCommand>(), It.IsAny<CancellationToken>(), null),
            Times.Exactly(2));
        bus.Verify(
            x => x.PublishAsync(It.IsAny<ReconcileGatewayPaymentsCronCommand>(), It.IsAny<DeliveryOptions>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_OneInvoiceThrows_OthersStillProcessed()
    {
        var invoiceRepo = new Mock<IInvoiceRepository>();
        var bus = new Mock<IMessageBus>();
        var a = Invoice.Create(1, DateTimeOffset.UtcNow.AddDays(7));
        a.SetGatewaySession("innovayse-inecobank", "gw-a");
        var b = Invoice.Create(2, DateTimeOffset.UtcNow.AddDays(7));
        b.SetGatewaySession("innovayse-inecobank", "gw-b");
        invoiceRepo.Setup(r => r.ListPendingGatewayPaymentsAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([a, b]);
        bus.SetupSequence(x => x.InvokeAsync<string>(It.IsAny<CompleteGatewayPaymentCommand>(), It.IsAny<CancellationToken>(), null))
            .ThrowsAsync(new InvalidOperationException("gateway down"))
            .ReturnsAsync("paid");
        bus.Setup(x => x.PublishAsync(It.IsAny<ReconcileGatewayPaymentsCronCommand>(), It.IsAny<DeliveryOptions>()))
            .Returns(ValueTask.CompletedTask);

        var handler = new ReconcileGatewayPaymentsCronHandler(
            invoiceRepo.Object, bus.Object, NullLogger<ReconcileGatewayPaymentsCronHandler>.Instance);
        await handler.HandleAsync(new ReconcileGatewayPaymentsCronCommand(), CancellationToken.None);

        bus.Verify(
            x => x.InvokeAsync<string>(It.IsAny<CompleteGatewayPaymentCommand>(), It.IsAny<CancellationToken>(), null),
            Times.Exactly(2));
    }
}
