using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Orders.Commands.ClaimOrderLines;
using Innovayse.Hostpanel.Application.Orders.Commands.RecordOrderLineOutcome;
using Innovayse.Hostpanel.Application.Orders.Common.Enums;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <see cref="ClaimOrderLinesCommand"/> through the real <see cref="IMessageBus"/>: pending lines become in progress with their
/// first attempt counted and the accepted order Incomplete, a failed line is claimed again with a new attempt, lines that are
/// claimed, fulfilled or asked not to be claimed are left alone, and an id that names nothing is refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ClaimOrderLinesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An order that does not exist is refused as <c>ORDER_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task MissingOrderIsRefused()
    {
        var result = await InvokeAsync(new ClaimOrderLinesCommand(987_654_321));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_NOT_FOUND);
    }

    /// <summary>A line the order does not hold is refused as <c>ORDER_ITEM_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task MissingLineIsRefused()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory, OrderStatus.Active);

        var result = await InvokeAsync(new ClaimOrderLinesCommand(orderId, 987_654_321));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_ITEM_NOT_FOUND);
    }

    /// <summary>
    /// Every pending line of an accepted order is claimed in one save: in progress, the first attempt counted and dated, and
    /// the order Incomplete instead of Active over lines that have not started.
    /// </summary>
    [Fact]
    public async Task ClaimsEveryPendingLineAndMakesTheOrderIncomplete()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        await AcceptWithoutFulfilmentAsync(seeded.OrderId);

        var result = await InvokeAsync(new ClaimOrderLinesCommand(seeded.OrderId));

        result.IsSuccess.Should().BeTrue();
        var order = await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, seeded.OrderId);
        order.Items.Should().OnlyContain(i => i.FulfilmentState == OrderItemFulfilmentState.InProgress
            && i.FulfilmentAttempts == 1
            && i.LastFulfilmentAttemptAt != null);
        order.Status.Should().Be(OrderStatus.Incomplete);
    }

    /// <summary>A claim for one line leaves the others pending, and claiming a line twice counts one attempt.</summary>
    [Fact]
    public async Task ClaimsOnlyTheNamedLineAndNeverTwice()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        await AcceptWithoutFulfilmentAsync(seeded.OrderId);
        var lines = await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId);

        (await InvokeAsync(new ClaimOrderLinesCommand(seeded.OrderId, lines[1].Id))).IsSuccess.Should().BeTrue();
        (await InvokeAsync(new ClaimOrderLinesCommand(seeded.OrderId, lines[1].Id))).IsSuccess.Should().BeTrue();

        var after = await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId);
        after[0].FulfilmentState.Should().Be(OrderItemFulfilmentState.Pending);
        after[1].FulfilmentState.Should().Be(OrderItemFulfilmentState.InProgress);
        after[1].FulfilmentAttempts.Should().Be(1);
    }

    /// <summary>A failed line is claimed again with a new attempt, and a fulfilled line is left as it is.</summary>
    [Fact]
    public async Task ClaimsAFailedLineAgainAndLeavesAFulfilledLineAlone()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory, OrderStatus.Active);
        var lineId = (await OrdersTestReads.ReadItemsAsync(factory.Services, orderId)).Single().Id;
        (await InvokeAsync(new ClaimOrderLinesCommand(orderId))).IsSuccess.Should().BeTrue();
        (await InvokeAsync(new RecordOrderLineOutcomeCommand(orderId, lineId, 1, OrderLineOutcome.Failed, "SOME_FAILURE"))).IsSuccess.Should().BeTrue();

        (await InvokeAsync(new ClaimOrderLinesCommand(orderId))).IsSuccess.Should().BeTrue();

        var claimed = (await OrdersTestReads.ReadItemsAsync(factory.Services, orderId)).Single();
        claimed.FulfilmentState.Should().Be(OrderItemFulfilmentState.InProgress);
        claimed.FulfilmentAttempts.Should().Be(2);
        (await InvokeAsync(new RecordOrderLineOutcomeCommand(orderId, lineId, 2, OrderLineOutcome.Fulfilled))).IsSuccess.Should().BeTrue();

        (await InvokeAsync(new ClaimOrderLinesCommand(orderId))).IsSuccess.Should().BeTrue();

        var done = (await OrdersTestReads.ReadItemsAsync(factory.Services, orderId)).Single();
        done.FulfilmentState.Should().Be(OrderItemFulfilmentState.Fulfilled);
        done.FulfilmentAttempts.Should().Be(2);
    }

    /// <summary>A non-positive id is refused by the validator.</summary>
    [Fact]
    public async Task InvalidIdsAreRefusedByTheValidator()
    {
        var act = async () => await InvokeAsync(new ClaimOrderLinesCommand(0, 0));

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    /// <summary>Accepts the order the way the admin does, but without running its fulfilment, so the lines are still pending.</summary>
    /// <param name="orderId">The pending order.</param>
    /// <returns>A task that completes once the order is accepted.</returns>
    private async Task AcceptWithoutFulfilmentAsync(int orderId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.Include(o => o.Items).FirstAsync(o => o.Id == orderId);
        order.Accept(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
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
