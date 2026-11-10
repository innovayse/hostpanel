using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <c>GET /api/orders/{id}</c> against Postgres: the happy path, the missing-order refusal, and
/// the authorization boundary (anonymous, wrong role).
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetOrderTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An id nothing names answers 404 with <c>ORDER_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MissingOrderRefusesNotFound()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        const int MissingId = 987_654_321;

        var response = await admin.GetAsync($"/api/orders/{MissingId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_NOT_FOUND);
    }

    /// <summary>A seeded pending order can be read back by id, in full detail, named after its client.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReturnsTheSeededOrdersDetail()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/orders/{orderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<JsonElement>();
        dto.GetProperty("id").GetInt32().Should().Be(orderId);
        dto.GetProperty("status").GetString().Should().Be(nameof(OrderStatus.Pending));
        dto.GetProperty("clientName").GetString().Should().Be("Ada Lovelace");
    }

    /// <summary>
    /// The server tells for each line whether staff may retry it or close it by hand: the failed domain line may be both
    /// retried and closed, the fulfilled hosting line neither, so the admin needs no lease of its own.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TellsForEachLineWhetherItMayBeRetriedOrClosed()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();

        var response = await admin.GetAsync($"/api/orders/{seeded.OrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray().ToList();
        var domainLine = items.Single(i => i.GetProperty("domain").ValueKind == JsonValueKind.String);
        var hostingLine = items.Single(i => i.GetProperty("domain").ValueKind == JsonValueKind.Null);
        domainLine.GetProperty("canRetry").GetBoolean().Should().BeTrue();
        domainLine.GetProperty("canClose").GetBoolean().Should().BeTrue();
        hostingLine.GetProperty("canRetry").GetBoolean().Should().BeFalse();
        hostingLine.GetProperty("canClose").GetBoolean().Should().BeFalse();
    }

    /// <summary>
    /// On a taxed invoice the order's own total stays the pre-tax sum of its lines, while the
    /// invoice total beside it carries the tax the client owes.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReturnsTheLineSumAndTheTaxedInvoiceTotalApart()
    {
        var seeded = await OrdersTestSeeds.SeedOrderWithInvoiceAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var invoice = await db.Invoices.SingleAsync(i => i.Id == seeded.InvoiceId);
            await db.InvoiceItems.Where(i => i.InvoiceId == seeded.InvoiceId).LoadAsync();
            invoice.UpdateOptions(invoice.InvoiceDate, invoice.DueDate, invoice.PaymentMethod, taxRate: 10m);
            await db.SaveChangesAsync();
        }

        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var dto = await admin.GetFromJsonAsync<JsonElement>($"/api/orders/{seeded.OrderId}");

        dto.GetProperty("total").GetDecimal().Should().Be(25m);
        dto.GetProperty("invoiceTotal").GetDecimal().Should().Be(27.5m);
    }

    /// <summary>An order without an invoice reads no invoice total.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ReturnsNoInvoiceTotalWithoutAnInvoice()
    {
        var seeded = await OrdersTestSeeds.SeedUninvoicedOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var dto = await admin.GetFromJsonAsync<JsonElement>($"/api/orders/{seeded.OrderId}");

        dto.GetProperty("invoiceTotal").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>Reading an order is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory);

        var response = await factory.CreateClient().GetAsync($"/api/orders/{orderId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client caller, who is not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAClientCaller()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory);
        var client = await OrdersTestSeeds.ClientCallerAsync(factory);

        var response = await client.GetAsync($"/api/orders/{orderId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
