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
/// <c>POST /api/orders/{id}/items/{itemId}/close-refund</c> against Postgres: staff who gave a line's cost back by hand (a
/// partly done refund, a failed line, a refund stuck under way) close the line as refunded, the order returns to Active, the
/// action is audited with the state the line was closed from, and a fulfilled line, an unknown id and a caller who is not an
/// admin are refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CloseOrderLineRefundTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// A partly refunded line is closed as refunded with 204: it no longer keeps the order incomplete (the order is Active
    /// again), the line cannot be retried, and the client's activity log records who did it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
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
            a => a.ClientId == seeded.ClientId
                && a.Description.Contains($"Closed Order Line Refund - Order ID: {seeded.OrderId} - Line ID: {domainLine} - From: PartiallyRefunded")))
            .Should().Be(1);
        var retry = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{domainLine}/retry", content: null);
        retry.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// A failed line staff refunded by hand, and a line whose refund is stuck in Refunding, are closed as refunded with 204:
    /// the order returns to Active, the audit line names the state, and the line can no longer be retried.
    /// </summary>
    /// <param name="stuckInRefunding">Whether the line is stuck in Refunding rather than failed.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosesAFailedOrStuckRefundingLine(bool stuckInRefunding)
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var failed = (await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId)).Single(i => i.Domain == seeded.Domain);
        if (stuckInRefunding)
        {
            using var claimScope = factory.Services.CreateScope();
            var bus = claimScope.ServiceProvider.GetRequiredService<IMessageBus>();
            (await bus.InvokeAsync<Result>(new BeginOrderLineRefundCommand(seeded.OrderId, failed.Id))).IsSuccess.Should().BeTrue();
            await BackdateRefundStartAsync(failed.Id, TimeSpan.FromDays(1));
        }

        var response = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{failed.Id}/close-refund", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var after = await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, seeded.OrderId);
        after.Items.Single(i => i.Id == failed.Id).FulfilmentState.Should().Be(OrderItemFulfilmentState.Refunded);
        after.Status.Should().Be(OrderStatus.Active);
        var from = stuckInRefunding ? nameof(OrderItemFulfilmentState.Refunding) : nameof(OrderItemFulfilmentState.Failed);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ActivityLogs.AsNoTracking().CountAsync(
            a => a.ClientId == seeded.ClientId
                && a.Description.Contains($"Closed Order Line Refund - Order ID: {seeded.OrderId} - Line ID: {failed.Id} - From: {from}")))
            .Should().Be(1);
        var retry = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{failed.Id}/retry", content: null);
        retry.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// A refund that began moments ago may still be giving the money back: closing it by hand is refused with 409 and
    /// <c>ORDER_LINE_REFUND_IN_PROGRESS</c>, the line stays Refunding, nothing is audited, and the order detail does not offer
    /// the close button for it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesARefundThatMayStillBeRunning()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var failed = (await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId)).Single(i => i.Domain == seeded.Domain);
        using (var claimScope = factory.Services.CreateScope())
        {
            var bus = claimScope.ServiceProvider.GetRequiredService<IMessageBus>();
            (await bus.InvokeAsync<Result>(new BeginOrderLineRefundCommand(seeded.OrderId, failed.Id))).IsSuccess.Should().BeTrue();
        }

        var response = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{failed.Id}/close-refund", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_LINE_REFUND_IN_PROGRESS);
        var after = await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, seeded.OrderId);
        after.Items.Single(i => i.Id == failed.Id).FulfilmentState.Should().Be(OrderItemFulfilmentState.Refunding);
        after.Status.Should().Be(OrderStatus.Incomplete);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ActivityLogs.AsNoTracking().CountAsync(
            a => a.ClientId == seeded.ClientId
                && a.Description.Contains($"Closed Order Line Refund - Order ID: {seeded.OrderId} - Line ID: {failed.Id}")))
            .Should().Be(0);
        using var detail = System.Text.Json.JsonDocument.Parse(await admin.GetStringAsync($"/api/orders/{seeded.OrderId}"));
        detail.RootElement.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("id").GetInt32() == failed.Id)
            .GetProperty("canClose").GetBoolean().Should().BeFalse();
    }

    /// <summary>A fulfilled line has nothing to give back: 409 with <c>ORDER_LINE_NOT_REFUNDABLE</c>, and it stays fulfilled.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAFulfilledLine()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var fulfilled = (await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId)).Single(i => i.Domain is null);

        var response = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{fulfilled.Id}/close-refund", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_LINE_NOT_REFUNDABLE);
        var after = await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, seeded.OrderId);
        after.Items.Single(i => i.Id == fulfilled.Id).FulfilmentState.Should().Be(OrderItemFulfilmentState.Fulfilled);
        after.Status.Should().Be(OrderStatus.Incomplete);
    }

    /// <summary>An order that does not exist answers 404 with <c>ORDER_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MissingOrderRefusesNotFound()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync("/api/orders/987654321/items/1/close-refund", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_NOT_FOUND);
    }

    /// <summary>A line the order does not hold answers 404 with <c>ORDER_ITEM_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
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
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task NonPositiveIdIsRefusedByTheValidator()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync("/api/orders/1/items/0/close-refund", content: null);

        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>A caller without a session is refused with 401.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnAnonymousCaller()
    {
        var response = await factory.CreateClient().PostAsync("/api/orders/1/items/1/close-refund", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client caller, who is not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAClientCaller()
    {
        var client = await OrdersTestSeeds.ClientCallerAsync(factory);

        var response = await client.PostAsync("/api/orders/1/items/1/close-refund", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Moves a refunding line's refund start into the past, the way a refund that died long ago looks, so it is stuck past
    /// the retry lease.
    /// </summary>
    /// <param name="lineId">The refunding line.</param>
    /// <param name="age">How long ago the refund is to have begun.</param>
    /// <returns>A task that completes when the line is updated.</returns>
    private async Task BackdateRefundStartAsync(int lineId, TimeSpan age)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var startedAt = DateTimeOffset.UtcNow - age;
        await db.OrderItems.Where(i => i.Id == lineId).ExecuteUpdateAsync(s => s
            .SetProperty(i => i.LastFulfilmentAttemptAt, startedAt));
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
