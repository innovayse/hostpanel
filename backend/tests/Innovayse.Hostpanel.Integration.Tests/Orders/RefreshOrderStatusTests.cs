using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Orders.Commands.RefreshOrderStatus;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <see cref="RefreshOrderStatusCommand"/> through the real <see cref="IMessageBus"/>: the order's status is brought in line
/// with its lines as the database holds them, an order that already fits is left as it is, a pending order is the admin's
/// decision and is not touched, and an id that names nothing is refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RefreshOrderStatusTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An order that does not exist is refused as <c>ORDER_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MissingOrderIsRefused()
    {
        var result = await InvokeAsync(new RefreshOrderStatusCommand(987_654_321));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_NOT_FOUND);
    }

    /// <summary>
    /// An order left Incomplete by a copy that still saw a line open, while every line is fulfilled in the database, is settled
    /// Active.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SettlesAnOrderWhoseLinesAreAllDone()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        (await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, seeded.OrderId)).Status.Should().Be(OrderStatus.Incomplete);
        await MarkEveryLineAsync(seeded.OrderId, OrderItemFulfilmentState.Fulfilled);

        var result = await InvokeAsync(new RefreshOrderStatusCommand(seeded.OrderId));

        result.IsSuccess.Should().BeTrue();
        (await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, seeded.OrderId)).Status.Should().Be(OrderStatus.Active);
    }

    /// <summary>An order with a line still failed stays Incomplete.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LeavesAnOrderWithAFailedLineIncomplete()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();

        var result = await InvokeAsync(new RefreshOrderStatusCommand(seeded.OrderId));

        result.IsSuccess.Should().BeTrue();
        (await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, seeded.OrderId)).Status.Should().Be(OrderStatus.Incomplete);
    }

    /// <summary>A pending order's status is the admin's decision and a refresh never changes it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task LeavesAPendingOrderPending()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        await MarkEveryLineAsync(seeded.OrderId, OrderItemFulfilmentState.Fulfilled);

        var result = await InvokeAsync(new RefreshOrderStatusCommand(seeded.OrderId));

        result.IsSuccess.Should().BeTrue();
        (await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, seeded.OrderId)).Status.Should().Be(OrderStatus.Pending);
    }

    /// <summary>Writes a state onto every line of the order behind the use cases' backs, as another run finishing would.</summary>
    /// <param name="orderId">The order.</param>
    /// <param name="state">The state to write.</param>
    /// <returns>A task that completes once the lines are written.</returns>
    private async Task MarkEveryLineAsync(int orderId, OrderItemFulfilmentState state)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.OrderItems.Where(i => i.OrderId == orderId).ExecuteUpdateAsync(s => s.SetProperty(i => i.FulfilmentState, state));
    }

    /// <summary>Dispatches the command on a fresh scope's bus.</summary>
    /// <param name="command">The command to send.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result> InvokeAsync(RefreshOrderStatusCommand command)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result>(command);
    }
}
