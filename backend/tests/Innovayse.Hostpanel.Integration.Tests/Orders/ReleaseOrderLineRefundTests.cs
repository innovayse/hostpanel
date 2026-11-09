using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Orders.Commands.BeginOrderLineRefund;
using Innovayse.Hostpanel.Application.Orders.Commands.ReleaseOrderLineRefund;
using Innovayse.Hostpanel.Application.Orders.Common.Enums;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <see cref="ReleaseOrderLineRefundCommand"/> through the real <see cref="IMessageBus"/>: a refund that returned nothing
/// puts the line back to failed and retryable, one that returned only part leaves it for staff, and a line that is not being
/// refunded or an id that names nothing is refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ReleaseOrderLineRefundTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An order that does not exist is refused as <c>ORDER_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MissingOrderIsRefused()
    {
        var result = await InvokeAsync(new ReleaseOrderLineRefundCommand(987_654_321, 1, OrderLineRefundOutcome.NothingRefunded));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_NOT_FOUND);
    }

    /// <summary>A line the order does not hold is refused as <c>ORDER_ITEM_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MissingLineIsRefused()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);

        var result = await InvokeAsync(new ReleaseOrderLineRefundCommand(seeded.OrderId, 987_654_321, OrderLineRefundOutcome.NothingRefunded));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_ITEM_NOT_FOUND);
    }

    /// <summary>A line whose refund was never begun is refused as <c>ORDER_LINE_NOT_REFUNDABLE</c> and left failed.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesALineThatIsNotBeingRefunded()
    {
        var (seeded, line, _) = await AcceptedOrderWithFailedLineAsync();

        var result = await InvokeAsync(new ReleaseOrderLineRefundCommand(seeded.OrderId, line.Id, OrderLineRefundOutcome.PartlyRefunded));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_LINE_NOT_REFUNDABLE);
        (await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId)).Single(i => i.Id == line.Id)
            .FulfilmentState.Should().Be(OrderItemFulfilmentState.Failed);
    }

    /// <summary>
    /// A refund that returned nothing puts the line back to Failed with its code, the order stays Incomplete, and the line can
    /// be retried again (the retry runs and answers 200).
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task NothingRefundedReturnsTheLineToFailedAndRetryable()
    {
        var (seeded, line, admin) = await AcceptedOrderWithFailedLineAsync();
        (await InvokeAsync(new BeginOrderLineRefundCommand(seeded.OrderId, line.Id))).IsSuccess.Should().BeTrue();

        var result = await InvokeAsync(new ReleaseOrderLineRefundCommand(seeded.OrderId, line.Id, OrderLineRefundOutcome.NothingRefunded));

        result.IsSuccess.Should().BeTrue();
        var order = await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, seeded.OrderId);
        var after = order.Items.Single(i => i.Id == line.Id);
        after.FulfilmentState.Should().Be(OrderItemFulfilmentState.Failed);
        after.FulfilmentErrorCode.Should().Be(HostpanelErrorCodes.DOMAIN_ALREADY_REGISTERED);
        order.Status.Should().Be(OrderStatus.Incomplete);
        var retry = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{line.Id}/retry", content: null);
        retry.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// A refund that returned only part leaves the line PartiallyRefunded: closed to retries (<c>ORDER_LINE_NOT_RETRYABLE</c>),
    /// not refundable a second time, and the order stays Incomplete until staff finish it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PartlyRefundedLeavesTheLineForManualAction()
    {
        var (seeded, line, admin) = await AcceptedOrderWithFailedLineAsync();
        (await InvokeAsync(new BeginOrderLineRefundCommand(seeded.OrderId, line.Id))).IsSuccess.Should().BeTrue();

        var result = await InvokeAsync(new ReleaseOrderLineRefundCommand(seeded.OrderId, line.Id, OrderLineRefundOutcome.PartlyRefunded));

        result.IsSuccess.Should().BeTrue();
        var order = await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, seeded.OrderId);
        order.Items.Single(i => i.Id == line.Id).FulfilmentState.Should().Be(OrderItemFulfilmentState.PartiallyRefunded);
        order.Status.Should().Be(OrderStatus.Incomplete);
        var retry = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{line.Id}/retry", content: null);
        retry.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await OrdersTestSeeds.AssertCodeAsync(retry, HostpanelErrorCodes.ORDER_LINE_NOT_RETRYABLE);
        (await InvokeAsync(new BeginOrderLineRefundCommand(seeded.OrderId, line.Id))).ErrorCode
            .Should().Be(HostpanelErrorCodes.ORDER_LINE_NOT_REFUNDABLE);
    }

    /// <summary>Accepts the taken-domain order, which leaves its domain line failed, and returns the order, that line and an admin client.</summary>
    /// <returns>The seeded order, the failed domain line and an authenticated admin client.</returns>
    private async Task<(TakenDomainOrder Seeded, OrderItem Line, HttpClient Admin)> AcceptedOrderWithFailedLineAsync()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var line = (await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId)).Single(i => i.Domain == seeded.Domain);
        return (seeded, line, admin);
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
