using Innovayse.Hostpanel.Application.Services.Queries.CountLiveServices;
using Innovayse.Hostpanel.Domain.Clients;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Domain.Services;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary><see cref="CountLiveServicesQuery"/> through the real <see cref="IMessageBus"/>.</summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class CountLiveServicesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>
    /// A terminated service is not counted, the client filter narrows the count, and a product with no service is
    /// answered with 0.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task HandleAsync_CountsOnlyServicesThatAreNotTerminatedAsync()
    {
        var productId = await OrdersTestSeeds.SeedProductAsync(factory);
        var emptyProductId = await OrdersTestSeeds.SeedProductAsync(factory);
        int clientA;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var a = Client.Create($"subject-{Guid.NewGuid():N}", "A", "A", $"{Guid.NewGuid():N}@example.test", TestMoments.Seeded);
            var b = Client.Create($"subject-{Guid.NewGuid():N}", "B", "B", $"{Guid.NewGuid():N}@example.test", TestMoments.Seeded);
            db.Clients.AddRange(a, b);
            await db.SaveChangesAsync();
            clientA = a.Id;

            var terminated = ClientService.Create(a.Id, productId, BillingCycle.Monthly, TestMoments.Seeded);
            terminated.Terminate(TestMoments.Seeded);
            db.ClientServices.AddRange(
                ClientService.Create(a.Id, productId, BillingCycle.Monthly, TestMoments.Seeded),
                ClientService.Create(b.Id, productId, BillingCycle.Monthly, TestMoments.Seeded),
                terminated);
            await db.SaveChangesAsync();
        }

        using var busScope = factory.Services.CreateScope();
        var bus = busScope.ServiceProvider.GetRequiredService<IMessageBus>();

        var all = await bus.InvokeAsync<Result<IReadOnlyDictionary<int, int>>>(
            new CountLiveServicesQuery([productId, emptyProductId]));
        var mine = await bus.InvokeAsync<Result<IReadOnlyDictionary<int, int>>>(
            new CountLiveServicesQuery([productId], clientA));

        all.Value![productId].Should().Be(2);
        all.Value[emptyProductId].Should().Be(0);
        mine.Value![productId].Should().Be(1);
    }
}
