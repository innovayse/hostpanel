using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Orders.Commands.FulfillInvoicedOrder;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <see cref="FulfillInvoicedOrderHandler"/> against Postgres: an invoice no order was raised for is
/// a no-op, and an invoice that pays a Pending order gets that order fulfilled.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class FulfillInvoicedOrderTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Seeds a client and an unpaid invoice for them.</summary>
    /// <param name="context">The context to seed through.</param>
    /// <returns>The invoice.</returns>
    private static async Task<Invoice> SeedInvoiceAsync(IHostpanelDbContext context)
    {
        var client = Client.Create($"subject-{Guid.NewGuid():N}", "Ada", "Lovelace", $"{Guid.NewGuid():N}@example.test", DateTimeOffset.UtcNow);
        context.Clients.Add(client);
        await context.SaveChangesAsync(CancellationToken.None);

        var invoice = Invoice.Create(client.Id, DateTimeOffset.UtcNow.AddDays(7), "USD", DateTimeOffset.UtcNow);
        invoice.AddItem("Manual", 10m, 1);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);

        return invoice;
    }

    /// <summary>An invoice that no order links to succeeds without touching any order.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InvoiceWithoutAnOrderIsANoOp()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await SeedInvoiceAsync(context);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await bus.InvokeAsync<Result>(new FulfillInvoicedOrderCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>The Pending order an invoice was raised for is accepted.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PendingOrderLinkedToTheInvoiceIsFulfilled()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await SeedInvoiceAsync(context);
        var order = Order.Create($"ORD-{Guid.NewGuid():N}"[..12], invoice.ClientId, "bank-gateway", ipAddress: null, currency: "USD", now: DateTimeOffset.UtcNow);
        context.Orders.Add(order);
        await context.SaveChangesAsync(CancellationToken.None);
        order.LinkInvoice(invoice.Id);
        await context.SaveChangesAsync(CancellationToken.None);
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var result = await bus.InvokeAsync<Result>(new FulfillInvoicedOrderCommand(invoice.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        using var verify = factory.Services.CreateScope();
        var reloaded = await verify.ServiceProvider.GetRequiredService<IHostpanelDbContext>().Orders.FindAsync(order.Id);
        reloaded!.Status.Should().Be(OrderStatus.Active);
    }
}
