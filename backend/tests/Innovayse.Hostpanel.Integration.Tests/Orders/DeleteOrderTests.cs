using System.Net;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <c>DELETE /api/orders/{id}</c> against Postgres: the happy path, the missing-order and
/// not-deletable refusals, and the authorization boundary (anonymous, wrong role).
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class DeleteOrderTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An id nothing names answers 404 with <c>ORDER_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task MissingOrderRefusesNotFound()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        const int MissingId = 987_654_321;

        var response = await admin.DeleteAsync($"/api/orders/{MissingId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_NOT_FOUND);
    }

    /// <summary>A pending order can be permanently deleted.</summary>
    [Fact]
    public async Task DeleteRemovesAPendingOrder()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.DeleteAsync($"/api/orders/{orderId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.GetAsync($"/api/orders/{orderId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Deleting a pending order cancels its open invoice through Billing in the same flow, so no
    /// invoice is left to be paid for an order that is gone.
    /// </summary>
    [Fact]
    public async Task DeleteCancelsThePendingOrdersOpenInvoice()
    {
        var seeded = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.DeleteAsync($"/api/orders/{seeded.OrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var invoice = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == seeded.InvoiceId);
        invoice.Status.Should().Be(InvoiceStatus.Cancelled);
        (await db.Orders.AsNoTracking().AnyAsync(o => o.Id == seeded.OrderId)).Should().BeFalse();
    }

    /// <summary>
    /// A pending order whose invoice already took the payment is not deleted: Billing refuses to
    /// cancel a paid invoice, and the order stays for its fulfilment.
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

        var response = await admin.DeleteAsync($"/api/orders/{seeded.OrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.INVOICE_CANCEL_NOT_ALLOWED);
        (await admin.GetAsync($"/api/orders/{seeded.OrderId}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>An active order may not be deleted; only Pending or Cancelled ones may.</summary>
    [Fact]
    public async Task RefusesAnActiveOrder()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory, OrderStatus.Active);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.DeleteAsync($"/api/orders/{orderId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_NOT_DELETABLE);
    }

    /// <summary>Deleting is not for anonymous callers.</summary>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory);

        var response = await factory.CreateClient().DeleteAsync($"/api/orders/{orderId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client caller, who is not an admin, is refused with 403.</summary>
    [Fact]
    public async Task RefusesAClientCaller()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory);
        var client = await OrdersTestSeeds.ClientCallerAsync(factory);

        var response = await client.DeleteAsync($"/api/orders/{orderId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
