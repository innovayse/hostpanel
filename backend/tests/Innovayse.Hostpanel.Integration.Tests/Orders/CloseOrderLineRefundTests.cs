using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Orders.Commands.BeginOrderLineRefund;
using Innovayse.Hostpanel.Application.Orders.Commands.ReleaseOrderLineRefund;
using Innovayse.Hostpanel.Application.Orders.Common.Enums;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <c>POST /api/orders/{id}/items/{itemId}/close-refund</c> against Postgres: staff who finished a partly done refund by hand
/// close the line as refunded, the order returns to Active, the action is audited, and a line in any other state, an unknown
/// id and a caller who is not an admin are refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CloseOrderLineRefundTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// A partly refunded line is closed as refunded with 204: it no longer keeps the order incomplete (the order is Active
    /// again), the line cannot be retried, and the client's activity log records who did it.
    /// </summary>
    [Fact]
    public async Task ClosesAPartlyRefundedLineAndTheOrderReturnsToActive()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var domainLine = await PartlyRefundDomainLineAsync(seeded.OrderId, seeded.Domain);

        var response = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{domainLine}/close-refund", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var order = await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, seeded.OrderId);
        order.Items.Single(i => i.Id == domainLine).FulfilmentState.Should().Be(OrderItemFulfilmentState.Refunded);
        order.Status.Should().Be(OrderStatus.Active);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ActivityLogs.AsNoTracking().CountAsync(
            a => a.ClientId == seeded.ClientId && a.Description.Contains($"Closed Order Line Refund - Order ID: {seeded.OrderId} - Line ID: {domainLine}")))
            .Should().Be(1);
        var retry = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{domainLine}/retry", content: null);
        retry.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>A failed line, whose refund never ran, cannot be closed as refunded: 409 with <c>ORDER_LINE_NOT_REFUNDABLE</c>.</summary>
    [Fact]
    public async Task RefusesALineThatIsNotPartlyRefunded()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var items = await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId);
        var failed = items.Single(i => i.Domain == seeded.Domain);
        var fulfilled = items.Single(i => i.Domain is null);

        var failedResponse = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{failed.Id}/close-refund", content: null);
        var fulfilledResponse = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{fulfilled.Id}/close-refund", content: null);

        failedResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await OrdersTestSeeds.AssertCodeAsync(failedResponse, HostpanelErrorCodes.ORDER_LINE_NOT_REFUNDABLE);
        fulfilledResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var after = await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, seeded.OrderId);
        after.Items.Single(i => i.Id == failed.Id).FulfilmentState.Should().Be(OrderItemFulfilmentState.Failed);
        after.Status.Should().Be(OrderStatus.Incomplete);
    }

    /// <summary>An order that does not exist answers 404 with <c>ORDER_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task MissingOrderRefusesNotFound()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync("/api/orders/987654321/items/1/close-refund", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_NOT_FOUND);
    }

    /// <summary>A line the order does not hold answers 404 with <c>ORDER_ITEM_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task MissingLineRefusesNotFound()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/987654321/close-refund", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_ITEM_NOT_FOUND);
    }

    /// <summary>A non-positive id is refused by the validator.</summary>
    [Fact]
    public async Task NonPositiveIdIsRefusedByTheValidator()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync("/api/orders/1/items/0/close-refund", content: null);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>A caller without a session is refused with 401.</summary>
    [Fact]
    public async Task RefusesAnAnonymousCaller()
    {
        var response = await factory.CreateClient().PostAsync("/api/orders/1/items/1/close-refund", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client caller, who is not an admin, is refused with 403.</summary>
    [Fact]
    public async Task RefusesAClientCaller()
    {
        var client = await OrdersTestSeeds.ClientCallerAsync(factory);

        var response = await client.PostAsync("/api/orders/1/items/1/close-refund", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Leaves the order's failed domain line partly refunded, the way a refund that returned only part of the cost does.
    /// </summary>
    /// <param name="orderId">The accepted order.</param>
    /// <param name="domain">The domain the failed line was for.</param>
    /// <returns>The line's id.</returns>
    private async Task<int> PartlyRefundDomainLineAsync(int orderId, string domain)
    {
        var line = (await OrdersTestReads.ReadItemsAsync(factory.Services, orderId)).Single(i => i.Domain == domain);
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        (await bus.InvokeAsync<Result>(new BeginOrderLineRefundCommand(orderId, line.Id))).IsSuccess.Should().BeTrue();
        (await bus.InvokeAsync<Result>(new ReleaseOrderLineRefundCommand(orderId, line.Id, OrderLineRefundOutcome.PartlyRefunded)))
            .IsSuccess.Should().BeTrue();
        (await OrdersTestReads.ReadItemsAsync(factory.Services, orderId)).Single(i => i.Id == line.Id)
            .FulfilmentState.Should().Be(OrderItemFulfilmentState.PartiallyRefunded);
        return line.Id;
    }
}
