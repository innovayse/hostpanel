using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Orders.Commands.ClaimOrderLines;
using Innovayse.Hostpanel.Application.Orders.Commands.RecordOrderLineOutcome;
using Innovayse.Hostpanel.Application.Orders.Commands.StartOrderLineAction;
using Innovayse.Hostpanel.Application.Orders.Common.Enums;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <see cref="StartOrderLineActionCommand"/> through the real <see cref="IMessageBus"/>: the attempt that holds a line may start
/// its action and the line is stamped; an attempt a newer one took over, a line nobody claimed and an id that names nothing are
/// refused, so a line is never run by two attempts.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class StartOrderLineActionTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An order that does not exist is refused as <c>ORDER_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MissingOrderIsRefused()
    {
        var result = await InvokeAsync(new StartOrderLineActionCommand(987_654_321, 1, 1));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_NOT_FOUND);
    }

    /// <summary>A line the order does not hold is refused as <c>ORDER_ITEM_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MissingLineIsRefused()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory, OrderStatus.Active);

        var result = await InvokeAsync(new StartOrderLineActionCommand(orderId, 987_654_321, 1));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_ITEM_NOT_FOUND);
    }

    /// <summary>
    /// The attempt that claimed the line starts its action: the line stays in progress under the same attempt and is stamped
    /// no earlier than the claim, so the lease counts from the start of the action.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheHoldingAttemptStartsAndStampsTheLine()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory, OrderStatus.Active);
        (await InvokeAsync(new ClaimOrderLinesCommand(orderId))).IsSuccess.Should().BeTrue();
        var claimed = (await OrdersTestReads.ReadItemsAsync(factory.Services, orderId)).Single();

        var result = await InvokeAsync(new StartOrderLineActionCommand(orderId, claimed.Id, 1));

        result.IsSuccess.Should().BeTrue();
        var line = (await OrdersTestReads.ReadItemsAsync(factory.Services, orderId)).Single();
        line.FulfilmentState.Should().Be(OrderItemFulfilmentState.InProgress);
        line.FulfilmentAttempts.Should().Be(1);
        line.LastFulfilmentAttemptAt.Should().BeOnOrAfter(claimed.LastFulfilmentAttemptAt!.Value);
    }

    /// <summary>
    /// An attempt whose line a newer attempt took over, and a line nobody claimed, are refused as <c>ORDER_LINE_CHANGED</c>,
    /// and the line is left as the newer attempt holds it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AStaleAttemptOrAnUnclaimedLineIsRefused()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory, OrderStatus.Active);
        var lineId = (await OrdersTestReads.ReadItemsAsync(factory.Services, orderId)).Single().Id;
        var unclaimed = await InvokeAsync(new StartOrderLineActionCommand(orderId, lineId, 1));
        (await InvokeAsync(new ClaimOrderLinesCommand(orderId))).IsSuccess.Should().BeTrue();
        (await InvokeAsync(new RecordOrderLineOutcomeCommand(orderId, lineId, 1, OrderLineOutcome.Failed, "FIRST_FAILURE"))).IsSuccess.Should().BeTrue();
        (await InvokeAsync(new ClaimOrderLinesCommand(orderId))).IsSuccess.Should().BeTrue();
        var before = (await OrdersTestReads.ReadItemsAsync(factory.Services, orderId)).Single();

        var stale = await InvokeAsync(new StartOrderLineActionCommand(orderId, lineId, 1));

        unclaimed.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_LINE_CHANGED);
        stale.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_LINE_CHANGED);
        var line = (await OrdersTestReads.ReadItemsAsync(factory.Services, orderId)).Single();
        line.FulfilmentAttempts.Should().Be(2);
        line.LastFulfilmentAttemptAt.Should().Be(before.LastFulfilmentAttemptAt);
    }

    /// <summary>A non-positive id or attempt is refused by the validator.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InvalidInputIsRefusedByTheValidator()
    {
        var act = async () => await InvokeAsync(new StartOrderLineActionCommand(0, 0, 0));

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
