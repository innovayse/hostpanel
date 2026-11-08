using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Orders.Commands.BeginOrderLineRefund;
using Innovayse.Hostpanel.Application.Orders.Commands.RecordOrderLineRefunded;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <see cref="RecordOrderLineRefundedCommand"/> through the real <see cref="IMessageBus"/>: a failed line whose
/// cost went back is closed and the order follows its lines, a line that is not failed is left as it is, a
/// closed line can no longer be retried, and an id that names nothing is refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RecordOrderLineRefundedTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An order that does not exist is refused as <c>ORDER_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task MissingOrderIsRefused()
    {
        var result = await InvokeAsync(new RecordOrderLineRefundedCommand(987_654_321, 1));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_NOT_FOUND);
    }

    /// <summary>A line the order does not hold is refused as <c>ORDER_ITEM_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task MissingLineIsRefused()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);

        var result = await InvokeAsync(new RecordOrderLineRefundedCommand(seeded.OrderId, 987_654_321));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_ITEM_NOT_FOUND);
    }

    /// <summary>
    /// A line whose refund was begun becomes Refunded, the order that only that line kept Incomplete returns to Active, and
    /// the closed line is refused by a retry with <c>ORDER_LINE_NOT_RETRYABLE</c>.
    /// </summary>
    [Fact]
    public async Task ClosesTheLineBeingRefundedAndTheOrderReturnsToActive()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var domainLine = (await ReadOrderAsync(seeded.OrderId)).Items.Single(i => i.Domain == seeded.Domain);
        (await InvokeAsync(new BeginOrderLineRefundCommand(seeded.OrderId, domainLine.Id))).IsSuccess.Should().BeTrue();

        var result = await InvokeAsync(new RecordOrderLineRefundedCommand(seeded.OrderId, domainLine.Id));

        result.IsSuccess.Should().BeTrue();
        var order = await ReadOrderAsync(seeded.OrderId);
        order.Items.Single(i => i.Id == domainLine.Id).FulfilmentState.Should().Be(OrderItemFulfilmentState.Refunded);
        order.Status.Should().Be(OrderStatus.Active);
        var retry = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{domainLine.Id}/retry", content: null);
        retry.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await OrdersTestSeeds.AssertCodeAsync(retry, HostpanelErrorCodes.ORDER_LINE_NOT_RETRYABLE);
    }

    /// <summary>A line that was fulfilled has nothing to give back: it is refused as <c>ORDER_LINE_NOT_REFUNDABLE</c> and left Fulfilled.</summary>
    [Fact]
    public async Task RefusesAFulfilledLineAndLeavesItAlone()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var hostingLine = (await ReadOrderAsync(seeded.OrderId)).Items.Single(i => i.Domain is null);

        var result = await InvokeAsync(new RecordOrderLineRefundedCommand(seeded.OrderId, hostingLine.Id));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_LINE_NOT_REFUNDABLE);
        var order = await ReadOrderAsync(seeded.OrderId);
        order.Items.Single(i => i.Id == hostingLine.Id).FulfilmentState.Should().Be(OrderItemFulfilmentState.Fulfilled);
        order.Status.Should().Be(OrderStatus.Incomplete);
    }

    /// <summary>A failed line whose refund was never begun is refused: money cannot be recorded as returned before it was claimed.</summary>
    [Fact]
    public async Task RefusesAFailedLineWhoseRefundWasNeverBegun()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var domainLine = (await ReadOrderAsync(seeded.OrderId)).Items.Single(i => i.Domain == seeded.Domain);

        var result = await InvokeAsync(new RecordOrderLineRefundedCommand(seeded.OrderId, domainLine.Id));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_LINE_NOT_REFUNDABLE);
        (await ReadOrderAsync(seeded.OrderId)).Items.Single(i => i.Id == domainLine.Id).FulfilmentState.Should().Be(OrderItemFulfilmentState.Failed);
    }

    /// <summary>A line that is Refunded already is left as it is and the command still succeeds, so a repeated message is harmless.</summary>
    [Fact]
    public async Task ARefundedLineIsLeftAsItIs()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var domainLine = (await ReadOrderAsync(seeded.OrderId)).Items.Single(i => i.Domain == seeded.Domain);
        (await InvokeAsync(new BeginOrderLineRefundCommand(seeded.OrderId, domainLine.Id))).IsSuccess.Should().BeTrue();
        (await InvokeAsync(new RecordOrderLineRefundedCommand(seeded.OrderId, domainLine.Id))).IsSuccess.Should().BeTrue();

        var again = await InvokeAsync(new RecordOrderLineRefundedCommand(seeded.OrderId, domainLine.Id));

        again.IsSuccess.Should().BeTrue();
        (await ReadOrderAsync(seeded.OrderId)).Items.Single(i => i.Id == domainLine.Id).FulfilmentState.Should().Be(OrderItemFulfilmentState.Refunded);
    }

    /// <summary>Reads an order with its lines from the database.</summary>
    /// <param name="orderId">The order.</param>
    /// <returns>The order and its lines.</returns>
    private async Task<Order> ReadOrderAsync(int orderId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Orders.AsNoTracking().Include(o => o.Items).SingleAsync(o => o.Id == orderId);
    }

    /// <summary>Dispatches the command on a fresh scope's bus.</summary>
    /// <param name="command">The command to send: the refund claim or its settlement.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result> InvokeAsync(object command)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result>(command);
    }
}
