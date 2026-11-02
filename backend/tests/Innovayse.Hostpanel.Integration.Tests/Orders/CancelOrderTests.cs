using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <c>POST /api/orders/{id}/cancel</c> against Postgres: the happy path, the missing-order
/// refusal, the linked invoice's cancellation or refusal, and the authorization boundary (anonymous, wrong role).
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CancelOrderTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An id nothing names answers 404 with <c>ORDER_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task MissingOrderRefusesNotFound()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        const int MissingId = 987_654_321;

        var response = await admin.PostAsync($"/api/orders/{MissingId}/cancel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_NOT_FOUND);
    }

    /// <summary>Cancelling a pending order moves it to Cancelled.</summary>
    [Fact]
    public async Task CancelMovesAPendingOrderToCancelled()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/orders/{orderId}/cancel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var read = await admin.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}");
        read.GetProperty("status").GetString().Should().Be(nameof(OrderStatus.Cancelled));
    }

    /// <summary>
    /// Cancelling a pending order cancels its open invoice through Billing in the same flow, so no
    /// invoice is left payable for an order that will never be fulfilled.
    /// </summary>
    [Fact]
    public async Task CancelCancelsThePendingOrdersOpenInvoice()
    {
        var seeded = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/orders/{seeded.OrderId}/cancel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invoice = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == seeded.InvoiceId);
        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == seeded.OrderId);
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    /// <summary>
    /// A pending order whose invoice already took the payment is not cancelled: Billing refuses to
    /// cancel a paid invoice, and the order stays Pending for its fulfilment.
    /// </summary>
    [Fact]
    public async Task RefusesAnOrderWhoseInvoiceIsPaid()
    {
        var seeded = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        using (var seedScope = factory.Services.CreateScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var paid = await seedDb.Invoices.SingleAsync(i => i.Id == seeded.InvoiceId);
            await seedDb.InvoiceItems.Where(i => i.InvoiceId == seeded.InvoiceId).LoadAsync();
            var payment = paid.Settle("bank-gateway", $"paid-{Guid.NewGuid():N}", DateTimeOffset.UtcNow, TransactionSource.Manual);
            seedDb.Transactions.Add(payment!);
            await seedDb.SaveChangesAsync();
        }

        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/orders/{seeded.OrderId}/cancel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.INVOICE_CANCEL_NOT_ALLOWED);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().SingleAsync(o => o.Id == seeded.OrderId);
        order.Status.Should().Be(OrderStatus.Pending);
    }

    /// <summary>An active order is refused with <c>ORDER_NOT_PENDING</c>; only Pending orders may be cancelled.</summary>
    [Fact]
    public async Task RefusesAnActiveOrder()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory, OrderStatus.Active);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/orders/{orderId}/cancel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_NOT_PENDING);
    }

    /// <summary>Cancelling is not for anonymous callers.</summary>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory);

        var response = await factory.CreateClient().PostAsync($"/api/orders/{orderId}/cancel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client caller, who is not an admin, is refused with 403.</summary>
    [Fact]
    public async Task RefusesAClientCaller()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory);
        var client = await OrdersTestSeeds.ClientCallerAsync(factory);

        var response = await client.PostAsync($"/api/orders/{orderId}/cancel", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
