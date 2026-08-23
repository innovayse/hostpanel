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
    /// <summary>Tolerance allowed between the asserted window bound and the expected instant, to absorb test execution time.</summary>
    private static readonly TimeSpan BoundTolerance = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task HandleAsync_CompletesEachPendingInvoice_AndReschedules()
    {
        var invoiceRepo = new Mock<IInvoiceRepository>();
        var bus = new Mock<IMessageBus>();
        var a = Invoice.Create(1, DateTimeOffset.UtcNow.AddDays(7));
        a.SetGatewaySession("innovayse-inecobank", "gw-a");
        var b = Invoice.Create(2, DateTimeOffset.UtcNow.AddDays(7));
        b.SetGatewaySession("innovayse-inecobank", "gw-b");

        DateTimeOffset capturedStartedAfter = default;
        DateTimeOffset capturedStartedBefore = default;
        invoiceRepo.Setup(r => r.ListPendingGatewayPaymentsAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback<DateTimeOffset, DateTimeOffset, CancellationToken>((startedAfter, startedBefore, _) =>
            {
                capturedStartedAfter = startedAfter;
                capturedStartedBefore = startedBefore;
            })
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
        var beforeCall = DateTimeOffset.UtcNow;
        await handler.HandleAsync(new ReconcileGatewayPaymentsCronCommand(), CancellationToken.None);

        bus.Verify(
            x => x.InvokeAsync<string>(It.IsAny<CompleteGatewayPaymentCommand>(), It.IsAny<CancellationToken>(), null),
            Times.Exactly(2));
        bus.Verify(
            x => x.PublishAsync(It.IsAny<ReconcileGatewayPaymentsCronCommand>(), It.IsAny<DeliveryOptions>()),
            Times.Once);

        // Pin the window bounds so a swapped AddHours(-24)/AddMinutes(-25) — which would compile,
        // pass the InvokeAsync/reschedule assertions above, and ship — actually fails here.
        Assert.True(
            Math.Abs((capturedStartedAfter - beforeCall.AddHours(-24)).TotalSeconds) < BoundTolerance.TotalSeconds,
            $"startedAfter {capturedStartedAfter:o} was not ~24h before {beforeCall:o}.");
        Assert.True(
            Math.Abs((capturedStartedBefore - beforeCall.AddMinutes(-25)).TotalSeconds) < BoundTolerance.TotalSeconds,
            $"startedBefore {capturedStartedBefore:o} was not ~25min before {beforeCall:o}.");
        Assert.True(
            capturedStartedAfter < capturedStartedBefore,
            "startedAfter (older bound) must be earlier than startedBefore (newer bound).");
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

        // The safety net must keep running even after a bad invoice — verify the reschedule
        // (via the same PublishAsync interception the extension method delegates to) still
        // happened exactly once, unconditionally, after the loop.
        bus.Verify(
            x => x.PublishAsync(It.IsAny<ReconcileGatewayPaymentsCronCommand>(), It.IsAny<DeliveryOptions>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_QueryThrows_StillReschedulesExactlyOnce()
    {
        // This is the only safety net for the whole no-webhook payment design — if the query
        // itself throws (not a per-invoice failure, but the run as a whole), the reschedule
        // must still happen in a `finally`, or the reconciler silently stops forever.
        var invoiceRepo = new Mock<IInvoiceRepository>();
        var bus = new Mock<IMessageBus>();
        invoiceRepo.Setup(r => r.ListPendingGatewayPaymentsAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));
        bus.Setup(x => x.PublishAsync(It.IsAny<ReconcileGatewayPaymentsCronCommand>(), It.IsAny<DeliveryOptions>()))
            .Returns(ValueTask.CompletedTask);

        var handler = new ReconcileGatewayPaymentsCronHandler(
            invoiceRepo.Object, bus.Object, NullLogger<ReconcileGatewayPaymentsCronHandler>.Instance);

        var exception = await Record.ExceptionAsync(
            () => handler.HandleAsync(new ReconcileGatewayPaymentsCronCommand(), CancellationToken.None));

        Assert.Null(exception);
        bus.Verify(
            x => x.InvokeAsync<string>(It.IsAny<CompleteGatewayPaymentCommand>(), It.IsAny<CancellationToken>(), null),
            Times.Never);
        bus.Verify(
            x => x.PublishAsync(It.IsAny<ReconcileGatewayPaymentsCronCommand>(), It.IsAny<DeliveryOptions>()),
            Times.Once);
    }
}
