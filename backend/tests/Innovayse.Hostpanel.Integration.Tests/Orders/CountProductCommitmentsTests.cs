using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Orders.Queries.CountProductCommitments;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary><see cref="CountProductCommitmentsQuery"/> through the real <see cref="IMessageBus"/>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CountProductCommitmentsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// An open order not yet fulfilled counts as a taken place, for every client and for its own client, and not
    /// for another client.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_CountsAnOpenOrderLineAsync()
    {
        await OrdersTestSeeds.EnableBankTransferAsync(factory);
        var productId = await OrdersTestSeeds.SeedProductAsync(factory);
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "commitments-a");
        var (_, otherClientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "commitments-b");

        var placed = await client.PostAsJsonAsync("/api/orders", new
        {
            paymentMethod = "bank_transfer",
            items = new[] { new { productId, billingCycle = nameof(BillingCycle.Monthly) } },
        });
        placed.StatusCode.Should().Be(HttpStatusCode.Created, await placed.Content.ReadAsStringAsync());

        using var scope = factory.Services.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var all = await bus.InvokeAsync<Result<IReadOnlyDictionary<int, int>>>(new CountProductCommitmentsQuery([productId]));
        var mine = await bus.InvokeAsync<Result<IReadOnlyDictionary<int, int>>>(new CountProductCommitmentsQuery([productId], clientId));
        var theirs = await bus.InvokeAsync<Result<IReadOnlyDictionary<int, int>>>(new CountProductCommitmentsQuery([productId], otherClientId));

        all.Value![productId].Should().Be(1);
        mine.Value![productId].Should().Be(1);
        theirs.Value![productId].Should().Be(0);
    }

    /// <summary>
    /// Once the service of an order line exists, the line and the service are one place, not two: the count stays at one
    /// whether the line is still open or fulfilled.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_CountsAnOrderLineAndItsServiceOnceAsync()
    {
        await OrdersTestSeeds.EnableBankTransferAsync(factory);
        var productId = await OrdersTestSeeds.SeedProductAsync(factory);
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "commitments-once");
        var placed = await client.PostAsJsonAsync("/api/orders", new
        {
            paymentMethod = "bank_transfer",
            items = new[] { new { productId, billingCycle = nameof(BillingCycle.Monthly) } },
        });
        placed.StatusCode.Should().Be(HttpStatusCode.Created, await placed.Content.ReadAsStringAsync());
        var orderId = (await placed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderId").GetInt32();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lineId = await db.OrderItems.AsNoTracking().Where(i => i.OrderId == orderId).Select(i => i.Id).SingleAsync();
        db.ClientServices.Add(ClientService.CreateOrdered(
            clientId, productId, BillingCycle.Monthly, null, 0m, 0m, null, null, TestMoments.Seeded, lineId));
        await db.SaveChangesAsync();

        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        var all = await bus.InvokeAsync<Result<IReadOnlyDictionary<int, int>>>(new CountProductCommitmentsQuery([productId]));

        all.Value![productId].Should().Be(1);
    }
}
