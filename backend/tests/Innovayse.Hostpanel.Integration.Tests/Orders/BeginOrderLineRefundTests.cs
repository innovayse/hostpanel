using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Orders.Commands.BeginOrderLineRefund;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <see cref="BeginOrderLineRefundCommand"/> through the real <see cref="IMessageBus"/>: only a failed line can be claimed
/// for its refund, the claim keeps a retry off the line, and a line that is not failed or an id that names nothing is refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class BeginOrderLineRefundTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An order that does not exist is refused as <c>ORDER_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task MissingOrderIsRefused()
    {
        var result = await InvokeAsync(new BeginOrderLineRefundCommand(987_654_321, 1));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_NOT_FOUND);
    }

    /// <summary>A line the order does not hold is refused as <c>ORDER_ITEM_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task MissingLineIsRefused()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);

        var result = await InvokeAsync(new BeginOrderLineRefundCommand(seeded.OrderId, 987_654_321));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_ITEM_NOT_FOUND);
    }

    /// <summary>
    /// A failed line becomes Refunding, the order stays Incomplete, and a retry of the line is refused with
    /// <c>ORDER_LINE_NOT_RETRYABLE</c> while the refund runs.
    /// </summary>
    [Fact]
    public async Task ClaimsTheFailedLineAndKeepsARetryOff()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var domainLine = (await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId)).Single(i => i.Domain == seeded.Domain);

        var result = await InvokeAsync(new BeginOrderLineRefundCommand(seeded.OrderId, domainLine.Id));

        result.IsSuccess.Should().BeTrue();
        var order = await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, seeded.OrderId);
        order.Items.Single(i => i.Id == domainLine.Id).FulfilmentState.Should().Be(OrderItemFulfilmentState.Refunding);
        order.Status.Should().Be(OrderStatus.Incomplete);
        var retry = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{domainLine.Id}/retry", content: null);
        retry.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await OrdersTestSeeds.AssertCodeAsync(retry, HostpanelErrorCodes.ORDER_LINE_NOT_RETRYABLE);
    }

    /// <summary>A line that is not failed (fulfilled here) is refused as <c>ORDER_LINE_NOT_REFUNDABLE</c> and left as it is.</summary>
    [Fact]
    public async Task RefusesALineThatDidNotFail()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var hostingLine = (await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId)).Single(i => i.Domain is null);

        var result = await InvokeAsync(new BeginOrderLineRefundCommand(seeded.OrderId, hostingLine.Id));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_LINE_NOT_REFUNDABLE);
        (await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId)).Single(i => i.Id == hostingLine.Id)
            .FulfilmentState.Should().Be(OrderItemFulfilmentState.Fulfilled);
    }

    /// <summary>A line already being refunded cannot be claimed a second time, so two refunds never run for one line.</summary>
    [Fact]
    public async Task RefusesALineThatIsAlreadyBeingRefunded()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var domainLine = (await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId)).Single(i => i.Domain == seeded.Domain);
        (await InvokeAsync(new BeginOrderLineRefundCommand(seeded.OrderId, domainLine.Id))).IsSuccess.Should().BeTrue();

        var second = await InvokeAsync(new BeginOrderLineRefundCommand(seeded.OrderId, domainLine.Id));

        second.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_LINE_NOT_REFUNDABLE);
    }

    /// <summary>A non-positive id is refused by the validator.</summary>
    [Fact]
    public async Task NonPositiveIdsAreRefusedByTheValidator()
    {
        var act = async () => await InvokeAsync(new BeginOrderLineRefundCommand(0, 0));

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    /// <summary>Dispatches the command on a fresh scope's bus.</summary>
    /// <param name="command">The command to send.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result> InvokeAsync(BeginOrderLineRefundCommand command)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result>(command);
    }
}
