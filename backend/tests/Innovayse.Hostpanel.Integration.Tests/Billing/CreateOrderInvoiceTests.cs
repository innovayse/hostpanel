using Innovayse.Hostpanel.Application.Billing.Commands.CreateOrderInvoice;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <see cref="CreateOrderInvoiceCommand"/> through the real <see cref="IMessageBus"/>: the invoice
/// the order flow has Billing raise for a newly placed order.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CreateOrderInvoiceTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>The snapshot the hosting line of the first test is sold as.</summary>
    private static readonly ProductSnapshot Snapshot = new(7, BillingCycle.Monthly, IsDigital: false, Sku: "STARTER", Metadata: null);

    /// <summary>The invoice is unpaid, in the order's currency, with one line per order line, due in a week.</summary>
    [Fact]
    public async Task HandleAsync_OrderLines_CreatesAnUnpaidInvoiceInTheOrdersCurrencyAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);

        Result<int> result;
        using (var scope = factory.Services.CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            result = await bus.InvokeAsync<Result<int>>(new CreateOrderInvoiceCommand(
                clientId, "USD", [new OrderInvoiceItemDto("Starter Hosting", 10m, 1, Snapshot), new OrderInvoiceItemDto("example.test", 5m, 1, null)]));
        }

        result.IsSuccess.Should().BeTrue();
        using var check = factory.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await context.Invoices.AsNoTracking().Include(i => i.Items).SingleAsync(i => i.Id == result.Value);
        invoice.ClientId.Should().Be(clientId);
        invoice.Status.Should().Be(InvoiceStatus.Unpaid);
        invoice.Currency.Should().Be("USD");
        invoice.Total.Should().Be(15m);
        invoice.Items.Should().HaveCount(2);
        invoice.Items.Single(i => i.Description == "Starter Hosting").ProductSnapshot.Should().Be(Snapshot);
        invoice.Items.Single(i => i.Description == "example.test").ProductSnapshot.Should().BeNull();
        invoice.DueDate.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(7), TimeSpan.FromMinutes(5));
    }

    /// <summary>The payment term comes from the <c>Orders</c> settings, not from the code.</summary>
    [Fact]
    public async Task HandleAsync_ConfiguredPaymentTerm_DatesTheInvoiceByItAsync()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        using var host = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Orders:InvoicePaymentTermDays", "30"));

        Result<int> result;
        using (var scope = host.Services.CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            result = await bus.InvokeAsync<Result<int>>(new CreateOrderInvoiceCommand(
                clientId, "USD", [new OrderInvoiceItemDto("Starter Hosting", 10m, 1, null)]));
        }

        result.IsSuccess.Should().BeTrue();
        using var check = host.Services.CreateScope();
        var context = check.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = await context.Invoices.AsNoTracking().SingleAsync(i => i.Id == result.Value);
        invoice.DueDate.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(30), TimeSpan.FromMinutes(5));
    }
}
