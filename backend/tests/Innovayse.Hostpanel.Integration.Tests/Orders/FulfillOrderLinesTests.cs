using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Orders.Commands.FulfillOrderLines;
using Innovayse.Hostpanel.Application.Orders.Common;
using Innovayse.Hostpanel.Application.Orders.Common.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <see cref="FulfillOrderLinesCommand"/> through the real <see cref="IMessageBus"/>: every line is
/// run the way its type asks, a failed line is reported and never stops the rest, and an unknown
/// order is refused.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class FulfillOrderLinesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An id nothing names is refused as <c>ORDER_NOT_FOUND</c>.</summary>
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
    [Fact]
    public async Task ReportsTheRefusedDomainLineAndFulfilsTheRest()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);

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
    [Fact]
    public async Task ActivatesTheDigitalLineWithoutProvisioningAndLeavesTheHostingLinePending()
    {
        var seeded = await OrdersTestSeeds.SeedDigitalAndHostingOrderAsync(factory);

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
