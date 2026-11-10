using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Orders.Commands.FulfillOrderLines;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Application.Orders.Common.Enums;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <see cref="FulfillOrderLinesCommand"/> through the real <see cref="IMessageBus"/>: every line is
/// run the way its type asks, a failed line is reported and never stops the rest, a line a retry took over while it waited
/// its turn is never run a second time, and an unknown or unaccepted order is refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class FulfillOrderLinesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An id nothing names is refused as <c>ORDER_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MissingOrderIsRefused()
    {
        var result = await InvokeAsync(new FulfillOrderLinesCommand(987_654_321));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_NOT_FOUND);
    }

    /// <summary>
    /// A refused domain registration is reported as a domain line, with its amount for a refund,
    /// and the hosting line after it still becomes a client service.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReportsTheRefusedDomainLineAndFulfilsTheRest()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        await OrdersTestSeeds.AcceptWithoutFulfilmentAsync(factory, seeded.OrderId);

        var result = await InvokeAsync(new FulfillOrderLinesCommand(seeded.OrderId));

        result.IsSuccess.Should().BeTrue();
        var failure = result.Value!.FailedLines.Should().ContainSingle().Which;
        failure.Kind.Should().Be(OrderLineKind.Domain);
        failure.Domain.Should().Be(seeded.Domain);
        failure.FirstPaymentAmount.Should().Be(10m);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var services = await db.ClientServices.AsNoTracking().Where(s => s.ClientId == seeded.ClientId).ToListAsync();
        services.Should().ContainSingle(s => s.ProductId == seeded.ProductId);
    }

    /// <summary>
    /// A digital line becomes an Active service at once, with no provisioning reference and no server, while a
    /// hosting line in the same order is created exactly as before: Pending, left for the client's setup.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ActivatesTheDigitalLineWithoutProvisioningAndLeavesTheHostingLinePending()
    {
        var seeded = await OrdersTestSeeds.SeedDigitalAndHostingOrderAsync(factory);
        await OrdersTestSeeds.AcceptWithoutFulfilmentAsync(factory, seeded.OrderId);

        var result = await InvokeAsync(new FulfillOrderLinesCommand(seeded.OrderId));

        result.IsSuccess.Should().BeTrue();
        result.Value!.FailedLines.Should().BeEmpty();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var services = await db.ClientServices.AsNoTracking().Where(s => s.ClientId == seeded.ClientId).ToListAsync();
        var digital = services.Should().ContainSingle(s => s.ProductId == seeded.DigitalProductId).Which;
        digital.Status.Should().Be(ServiceStatus.Active);
        digital.NextRenewalAt.Should().NotBeNull();
        digital.ProvisioningRef.Should().BeNull();
        digital.ServerId.Should().BeNull();
        var hosting = services.Should().ContainSingle(s => s.ProductId == seeded.HostingProductId).Which;
        hosting.Status.Should().Be(ServiceStatus.Pending);
        hosting.NextRenewalAt.Should().BeNull();
    }

    /// <summary>
    /// Running the fulfilment again over an order whose lines are fulfilled creates nothing a second time
    /// and counts no new attempt.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ALineAlreadyFulfilledIsNeverFulfilledAgain()
    {
        var seeded = await OrdersTestSeeds.SeedDigitalAndHostingOrderAsync(factory);
        await OrdersTestSeeds.AcceptWithoutFulfilmentAsync(factory, seeded.OrderId);
        (await InvokeAsync(new FulfillOrderLinesCommand(seeded.OrderId))).IsSuccess.Should().BeTrue();

        var again = await InvokeAsync(new FulfillOrderLinesCommand(seeded.OrderId));

        again.IsSuccess.Should().BeTrue();
        again.Value!.FailedLines.Should().BeEmpty();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ClientServices.AsNoTracking().CountAsync(s => s.ClientId == seeded.ClientId)).Should().Be(2);
        var items = (await db.Orders.AsNoTracking().Include(o => o.Items).SingleAsync(o => o.Id == seeded.OrderId)).Items;
        items.Should().OnlyContain(i => i.FulfilmentState == OrderItemFulfilmentState.Fulfilled && i.FulfilmentAttempts == 1);
    }

    /// <summary>A retry that names a line the order does not hold is refused with <c>ORDER_ITEM_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnUnknownLineIsRefused()
    {
        var seeded = await OrdersTestSeeds.SeedDigitalAndHostingOrderAsync(factory);

        var result = await InvokeAsync(new FulfillOrderLinesCommand(seeded.OrderId, 987_654_321));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_ITEM_NOT_FOUND);
    }

    /// <summary>
    /// An order that was never accepted has no lines to fulfil: the run is refused with <c>ORDER_NOT_ACCEPTED</c>, no line
    /// is claimed and no service is created.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APendingOrderIsRefused()
    {
        var seeded = await OrdersTestSeeds.SeedDigitalAndHostingOrderAsync(factory);

        var result = await InvokeAsync(new FulfillOrderLinesCommand(seeded.OrderId));

        result.ErrorCode.Should().Be(HostpanelErrorCodes.ORDER_NOT_ACCEPTED);
        var order = await OrdersTestReads.ReadOrderWithItemsAsync(factory.Services, seeded.OrderId);
        order.Status.Should().Be(OrderStatus.Pending);
        order.Items.Should().OnlyContain(i => i.FulfilmentState == OrderItemFulfilmentState.Pending && i.FulfilmentAttempts == 0);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ClientServices.AsNoTracking().CountAsync(s => s.ClientId == seeded.ClientId)).Should().Be(0);
    }

    /// <summary>
    /// Every line is claimed at once and the lines run one after another. A staff retry that takes a waiting line over (its
    /// lease ran out before its turn) owns it from then on: the first run checks its claim right before the line's action,
    /// finds the newer attempt and leaves the line alone, so the line's service is never created by the first run.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ALineARetryTookOverWhileItWaitedIsNotRunAgain()
    {
        var seeded = await OrdersTestSeeds.SeedDigitalAndHostingOrderAsync(factory);
        await OrdersTestSeeds.AcceptWithoutFulfilmentAsync(factory, seeded.OrderId);
        var interceptor = new TakeOverOnFirstActionInterceptor();
        using var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
            s.ConfigureDbContext<AppDbContext>(o => o.AddInterceptors(interceptor))));
        interceptor.Services = host.Services;
        using var scope = host.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await bus.InvokeAsync<Result<OrderFulfillmentDto>>(new FulfillOrderLinesCommand(seeded.OrderId));

        result.IsSuccess.Should().BeTrue();
        interceptor.TakenOverItemId.Should().NotBeNull();
        var taken = (await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId)).Single(i => i.Id == interceptor.TakenOverItemId);
        taken.FulfilmentState.Should().Be(OrderItemFulfilmentState.InProgress);
        taken.FulfilmentAttempts.Should().Be(2);
        using var readScope = factory.Services.CreateScope();
        var db = readScope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ClientServices.AsNoTracking().CountAsync(s => s.OrderItemId == interceptor.TakenOverItemId)).Should().Be(0);
    }

    /// <summary>Dispatches the command on a fresh scope's bus.</summary>
    /// <param name="command">The command to send.</param>
    /// <returns>The handler's result.</returns>
    private async Task<Result<OrderFulfillmentDto>> InvokeAsync(FulfillOrderLinesCommand command)
    {
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        return await bus.InvokeAsync<Result<OrderFulfillmentDto>>(command);
    }
}
