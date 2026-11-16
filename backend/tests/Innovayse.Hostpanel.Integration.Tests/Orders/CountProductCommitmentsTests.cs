using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Orders.Queries.CountProductCommitments;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Shared.Models;
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
}
