using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Fraud.Enums;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary><c>POST /api/orders/{id}/reject-review</c> against Postgres: the refusal of a held order, its invoice, the fraud log and the refusals of the call.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RejectOrderReviewTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Rejecting a held order flags it as fraud, cancels its open invoice and logs a manual rejection with the order's own address and score.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RejectsTheOrderAndCancelsItsInvoice()
    {
        var seeded = await FraudTestSeeds.SeedHeldOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync($"/api/orders/{seeded.OrderId}/reject-review", new { reason = "Card could not be verified." });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().Include(o => o.Items).SingleAsync(o => o.Id == seeded.OrderId);
        order.Status.Should().Be(OrderStatus.Fraud);
        order.Items.Should().OnlyContain(i => i.Status == OrderItemStatus.Cancelled);
        (await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == seeded.InvoiceId)).Status.Should().Be(InvoiceStatus.Cancelled);
        var logged = await db.FraudLogEntries.AsNoTracking().SingleAsync(l => l.OrderId == seeded.OrderId);
        logged.Decision.Should().Be(FraudDecision.ManualRejected);
        logged.Score.Should().Be(65);
    }

    /// <summary>An order whose invoice already took the payment is not rejected: Billing refuses to cancel it and the order stays under review for a refund first.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnOrderWhoseInvoiceIsPaid()
    {
        var seeded = await FraudTestSeeds.SeedHeldOrderAsync(factory, paid: true);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync($"/api/orders/{seeded.OrderId}/reject-review", new { reason = "Stolen card." });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.INVOICE_CANCEL_NOT_ALLOWED);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Orders.AsNoTracking().SingleAsync(o => o.Id == seeded.OrderId)).Status.Should().Be(OrderStatus.UnderReview);
    }

    /// <summary>The reason is required and bounded, because the client reads it.</summary>
    /// <param name="reason">The reason sent.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RefusesAMissingReason(string reason)
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory, OrderStatus.UnderReview);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync($"/api/orders/{orderId}/reject-review", new { reason });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A reason past the limit is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesATooLongReason()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory, OrderStatus.UnderReview);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync($"/api/orders/{orderId}/reject-review", new { reason = new string('x', 1001) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>An order that is not held is refused with <c>ORDER_NOT_UNDER_REVIEW</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnOrderThatIsNotUnderReview()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync($"/api/orders/{orderId}/reject-review", new { reason = "No." });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_NOT_UNDER_REVIEW);
    }

    /// <summary>An id nothing names answers 404 with <c>ORDER_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MissingOrderRefusesNotFound()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsJsonAsync("/api/orders/987654321/reject-review", new { reason = "No." });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_NOT_FOUND);
    }

    /// <summary>Rejecting is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory, OrderStatus.UnderReview);

        var response = await factory.CreateClient().PostAsJsonAsync($"/api/orders/{orderId}/reject-review", new { reason = "No." });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client caller, who is not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAClientCaller()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory, OrderStatus.UnderReview);
        var client = await OrdersTestSeeds.ClientCallerAsync(factory);

        var response = await client.PostAsJsonAsync($"/api/orders/{orderId}/reject-review", new { reason = "No." });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
