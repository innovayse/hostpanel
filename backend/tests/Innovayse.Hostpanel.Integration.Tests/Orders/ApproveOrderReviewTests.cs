using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary><c>POST /api/orders/{id}/approve-review</c> against Postgres: the release from the queue, the staff decision in the fraud log and the refusals.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ApproveOrderReviewTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Approving an unpaid held order returns it to Pending, keeps its score and logs a manual approval.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ApprovesAnUnpaidOrderBackToPending()
    {
        var seeded = await FraudTestSeeds.SeedHeldOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/orders/{seeded.OrderId}/approve-review", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == seeded.OrderId);
        order.Status.Should().Be(OrderStatus.Pending);
        order.FraudScore.Should().Be(65);
        var logged = await db.FraudLogEntries.AsNoTracking().SingleAsync(l => l.OrderId == seeded.OrderId);
        logged.Decision.Should().Be(FraudDecision.ManualApproved);
        logged.Score.Should().Be(65);
        logged.Reasons.Should().Equal(FraudReasonCode.DisposableEmail, FraudReasonCode.OrderVelocity);
    }

    /// <summary>Approving an order whose invoice was paid while it waited fulfils it at once, since nothing else would.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ApprovingAPaidOrderFulfilsIt()
    {
        var seeded = await FraudTestSeeds.SeedHeldOrderAsync(factory, paid: true);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/orders/{seeded.OrderId}/approve-review", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var status = await db.Orders.AsNoTracking().Where(o => o.Id == seeded.OrderId).Select(o => o.Status).SingleAsync();
        status.Should().BeOneOf(OrderStatus.Active, OrderStatus.Incomplete);
    }

    /// <summary>A payment that arrives while the order is held fulfils nothing: the order stays under review.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APaymentWhileHeldFulfilsNothing()
    {
        var seeded = await FraudTestSeeds.SeedHeldOrderAsync(factory, paid: true);
        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<Wolverine.IMessageBus>();

        await bus.InvokeAsync(new Innovayse.Hostpanel.Application.Orders.Commands.FulfillInvoicedOrder.FulfillInvoicedOrderCommand(seeded.InvoiceId));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Orders.AsNoTracking().Where(o => o.Id == seeded.OrderId).Select(o => o.Status).SingleAsync())
            .Should().Be(OrderStatus.UnderReview);
    }

    /// <summary>An order that is not held is refused with <c>ORDER_NOT_UNDER_REVIEW</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnOrderThatIsNotUnderReview()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/orders/{orderId}/approve-review", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_NOT_UNDER_REVIEW);
    }

    /// <summary>An id nothing names answers 404 with <c>ORDER_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MissingOrderRefusesNotFound()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync("/api/orders/987654321/approve-review", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_NOT_FOUND);
    }

    /// <summary>Approving is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory, OrderStatus.UnderReview);

        var response = await factory.CreateClient().PostAsync($"/api/orders/{orderId}/approve-review", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client caller, who is not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAClientCaller()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory, OrderStatus.UnderReview);
        var client = await OrdersTestSeeds.ClientCallerAsync(factory);

        var response = await client.PostAsync($"/api/orders/{orderId}/approve-review", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>The queue lists the held order with its score, reasons and address through the order list filtered by status.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheQueueShowsTheHeldOrderWithItsScoreAndReasons()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory, OrderStatus.UnderReview);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var page = await admin.GetFromJsonAsync<JsonElement>("/api/orders?page=1&pageSize=100&status=UnderReview");
        var detail = await admin.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}");

        var row = page.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == orderId);
        row.GetProperty("status").GetString().Should().Be("UnderReview");
        row.GetProperty("fraudScore").GetInt32().Should().Be(60);
        row.GetProperty("fraudReasons").EnumerateArray().Select(r => r.GetString()).Should().Equal("DisposableEmail");
        detail.GetProperty("fraudScore").GetInt32().Should().Be(60);
        detail.GetProperty("fraudReasons").EnumerateArray().Select(r => r.GetString()).Should().Equal("DisposableEmail");
    }
}
