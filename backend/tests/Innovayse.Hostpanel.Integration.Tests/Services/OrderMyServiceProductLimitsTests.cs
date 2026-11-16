using System.Net;
using System.Net.Http.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Products.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Services;

/// <summary>
/// <c>POST /api/me/services</c> against a product's cap on live services and its one-per-client rule: the service a
/// client orders for itself is held to the same limits as an order, through the same check.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class OrderMyServiceProductLimitsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Once a capped product's places are taken, the next client who orders it for itself is refused as sold out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Order_CapReached_RefusesSoldOutAsync()
    {
        var productId = await OrdersTestSeeds.SeedLimitedProductAsync(factory, max: 1, onePerClient: false);
        var (first, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "my-cap-a");
        var (second, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "my-cap-b");

        (await OrderAsync(first, productId)).StatusCode.Should().Be(HttpStatusCode.Created);

        var refused = await OrderAsync(second, productId);
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(refused, HostpanelErrorCodes.ORDER_PRODUCT_SOLD_OUT);
    }

    /// <summary>A client who already holds a one-per-client product cannot order it again; another client can.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Order_OnePerClient_RefusesTheSecondServiceOfTheSameClientAsync()
    {
        var productId = await OrdersTestSeeds.SeedLimitedProductAsync(factory, max: null, onePerClient: true);
        var (first, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "my-one-a");
        var (other, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "my-one-b");

        (await OrderAsync(first, productId)).StatusCode.Should().Be(HttpStatusCode.Created);

        var again = await OrderAsync(first, productId);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(again, HostpanelErrorCodes.ORDER_PRODUCT_ONE_PER_CLIENT);
        (await OrderAsync(other, productId)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>More clients ordering a capped product at the same moment than it has places take exactly the places there are.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task Order_ParallelOrdersAgainstACap_TakeExactlyTheCapAsync()
    {
        const int Parallel = 5;
        const int Cap = Parallel - 1;
        var productId = await OrdersTestSeeds.SeedLimitedProductAsync(factory, max: Cap, onePerClient: false);
        var clients = new List<HttpClient>();
        for (var i = 0; i < Parallel; i++)
        {
            clients.Add((await ClientsTestSeeds.RegisterClientAsync(factory, $"my-parallel-{i}")).Client);
        }

        var responses = await Task.WhenAll(clients.Select(client => OrderAsync(client, productId)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(Cap);
        var refused = responses.Where(r => r.StatusCode != HttpStatusCode.Created).ToList();
        refused.Should().ContainSingle().Which.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ClientsTestSeeds.AssertCodeAsync(refused[0], HostpanelErrorCodes.ORDER_PRODUCT_SOLD_OUT);
    }

    /// <summary>Orders the product for the signed-in client through the portal route.</summary>
    /// <param name="client">The signed-in client.</param>
    /// <param name="productId">The product to order.</param>
    /// <returns>The API's response.</returns>
    private static Task<HttpResponseMessage> OrderAsync(HttpClient client, int productId)
    {
        return client.PostAsJsonAsync("/api/me/services", new
        {
            productId,
            billingCycle = nameof(BillingCycle.Monthly),
        });
    }
}
