using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Orders.Commands.BeginOrderLineRefund;
using Innovayse.Hostpanel.Application.Orders.Commands.ClaimOrderLines;
using Innovayse.Hostpanel.Application.Orders.Commands.RecordOrderLineOutcome;
using Innovayse.Hostpanel.Application.Orders.Common.Enums;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <see cref="RecordOrderLineOutcomeCommand"/> through the real <see cref="IMessageBus"/>: a failure keeps its code and makes
/// the accepted order Incomplete, a success clears it and lets the order return to Active, an attempt the refund overtook is
/// refused, a late outcome of an attempt a newer one took over is refused and leaves the newer result, and an id that names
/// nothing is refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RecordOrderLineOutcomeTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An order that does not exist is refused as <c>ORDER_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MissingOrderIsRefused()
    {
        var result = await InvokeAsync(new RecordOrderLineOutcomeCommand(987_654_321, 1, 1, OrderLineOutcome.Fulfilled));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_NOT_FOUND);
    }

    /// <summary>A line the order does not hold is refused as <c>ORDER_ITEM_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MissingLineIsRefused()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory);

        var result = await InvokeAsync(new RecordOrderLineOutcomeCommand(orderId, 987_654_321, 1, OrderLineOutcome.Fulfilled));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_ITEM_NOT_FOUND);
    }

    /// <summary>
    /// A failure of the claimed attempt is stored with its code and makes the accepted order Incomplete; the retry's claim
    /// counts the second attempt, and its success clears the code and returns the order to Active.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RecordsAFailureThenASuccessAndTheOrderFollowsItsLine()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory, OrderStatus.Active);
        (await InvokeAsync(new ClaimOrderLinesCommand(orderId))).IsSuccess.Should().BeTrue();
        var lineId = (await OrdersTestReads.ReadItemsAsync(factory.Services, orderId)).Single().Id;

        var failed = await InvokeAsync(new RecordOrderLineOutcomeCommand(orderId, lineId, 1, OrderLineOutcome.Failed, "REGISTRAR_DOMAIN_UNAVAILABLE"));

        failed.IsSuccess.Should().BeTrue();
        var afterFailure = await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, orderId);
        var line = afterFailure.Items.Single();
        line.FulfilmentState.Should().Be(OrderItemFulfilmentState.Failed);
        line.FulfilmentErrorCode.Should().Be("REGISTRAR_DOMAIN_UNAVAILABLE");
        line.FulfilmentAttempts.Should().Be(1);
        afterFailure.Status.Should().Be(OrderStatus.Incomplete);

        (await InvokeAsync(new ClaimOrderLinesCommand(orderId))).IsSuccess.Should().BeTrue();
        var fulfilled = await InvokeAsync(new RecordOrderLineOutcomeCommand(orderId, lineId, 2, OrderLineOutcome.Fulfilled));

        fulfilled.IsSuccess.Should().BeTrue();
        var afterSuccess = await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, orderId);
        var done = afterSuccess.Items.Single();
        done.FulfilmentState.Should().Be(OrderItemFulfilmentState.Fulfilled);
        done.FulfilmentErrorCode.Should().BeNull();
        done.FulfilmentAttempts.Should().Be(2);
        afterSuccess.Status.Should().Be(OrderStatus.Active);
    }

    /// <summary>
    /// A late outcome of an attempt whose claim was taken over by a newer attempt is refused as <c>ORDER_LINE_CHANGED</c>
    /// and changes nothing, so it cannot overwrite what the newer attempt recorded; an outcome for a line nobody claimed is
    /// refused the same way.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnOutcomeFromAStaleAttemptIsRefusedAndTheNewerResultStays()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory, OrderStatus.Active);
        var lineId = (await OrdersTestReads.ReadItemsAsync(factory.Services, orderId)).Single().Id;
        var unclaimed = await InvokeAsync(new RecordOrderLineOutcomeCommand(orderId, lineId, 1, OrderLineOutcome.Fulfilled));
        unclaimed.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_LINE_CHANGED);

        (await InvokeAsync(new ClaimOrderLinesCommand(orderId))).IsSuccess.Should().BeTrue();
        (await InvokeAsync(new RecordOrderLineOutcomeCommand(orderId, lineId, 1, OrderLineOutcome.Failed, "FIRST_FAILURE"))).IsSuccess.Should().BeTrue();
        (await InvokeAsync(new ClaimOrderLinesCommand(orderId))).IsSuccess.Should().BeTrue();
        (await InvokeAsync(new RecordOrderLineOutcomeCommand(orderId, lineId, 2, OrderLineOutcome.Failed, "NEWER_FAILURE"))).IsSuccess.Should().BeTrue();

        var late = await InvokeAsync(new RecordOrderLineOutcomeCommand(orderId, lineId, 1, OrderLineOutcome.Fulfilled));
        var lateFailure = await InvokeAsync(new RecordOrderLineOutcomeCommand(orderId, lineId, 2, OrderLineOutcome.Fulfilled));

        late.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_LINE_CHANGED);
        lateFailure.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_LINE_CHANGED);
        var after = await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, orderId);
        var line = after.Items.Single();
        line.FulfilmentState.Should().Be(OrderItemFulfilmentState.Failed);
        line.FulfilmentErrorCode.Should().Be("NEWER_FAILURE");
        line.FulfilmentAttempts.Should().Be(2);
        after.Status.Should().Be(OrderStatus.Incomplete);
    }

    /// <summary>
    /// An attempt that finishes after the line's refund began cannot record itself: it is refused as <c>ORDER_LINE_CHANGED</c>
    /// and the line stays Refunding, so a late success never turns a refunded line into a delivered one.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnAttemptTheRefundOvertookIsRefused()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var domainLine = (await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId)).Single(i => i.Domain == seeded.Domain);
        (await InvokeAsync(new BeginOrderLineRefundCommand(seeded.OrderId, domainLine.Id))).IsSuccess.Should().BeTrue();

        var late = await InvokeAsync(new RecordOrderLineOutcomeCommand(seeded.OrderId, domainLine.Id, domainLine.FulfilmentAttempts, OrderLineOutcome.Fulfilled));

        late.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_LINE_CHANGED);
        (await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId)).Single(i => i.Id == domainLine.Id)
            .FulfilmentState.Should().Be(OrderItemFulfilmentState.Refunding);
    }

    /// <summary>A non-positive id or attempt, or an undefined outcome, is refused by the validator.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InvalidInputIsRefusedByTheValidator()
    {
        var act = async () => await InvokeAsync(new RecordOrderLineOutcomeCommand(0, 0, 0, (OrderLineOutcome)99));

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    /// <summary>Dispatches the command on a fresh scope's bus.</summary>
    /// <param name="command">The command to send.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result> InvokeAsync(object command)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result>(command);
    }
}
