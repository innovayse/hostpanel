using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <c>POST /api/orders</c> against a product's cap on live services and its one-per-client rule: an open order counts
/// as a taken place, a client may not order a one-per-client product twice, and a sold-out product is refused with 409
/// and left off the public catalogue.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class PlaceOrderProductLimitsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A client who has ordered a one-per-client product cannot order it again; another client can.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PlaceOrder_OnePerClient_RefusesTheSecondOrderOfTheSameClientAsync()
    {
        var productId = await SeedLimitedProductAsync(max: null, onePerClient: true);
        var (first, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "one-per-client-a");
        var (other, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "one-per-client-b");

        (await OrderAsync(first, productId)).StatusCode.Should().Be(HttpStatusCode.Created);

        var again = await OrderAsync(first, productId);
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await OrdersTestSeeds.AssertCodeAsync(again, HostpanelErrorCodes.ORDER_PRODUCT_ONE_PER_CLIENT);

        (await OrderAsync(other, productId)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>One order naming a one-per-client product twice is refused.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PlaceOrder_OnePerClient_RefusesTwoLinesInOneOrderAsync()
    {
        var productId = await SeedLimitedProductAsync(max: null, onePerClient: true);
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "one-per-client-twice");

        var response = await OrderAsync(client, productId, lines: 2);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_PRODUCT_ONE_PER_CLIENT);
    }

    /// <summary>
    /// Once a capped product's places are taken by open orders it is refused as sold out and the public catalogue
    /// no longer lists it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PlaceOrder_CapReached_IsSoldOutAndOffTheCatalogueAsync()
    {
        var productId = await SeedLimitedProductAsync(max: 1, onePerClient: false);
        var (first, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "capped-a");
        var (second, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "capped-b");

        (await ListedAsync(productId)).Should().BeTrue();
        (await OrderAsync(first, productId)).StatusCode.Should().Be(HttpStatusCode.Created);

        var refused = await OrderAsync(second, productId);
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await OrdersTestSeeds.AssertCodeAsync(refused, HostpanelErrorCodes.ORDER_PRODUCT_SOLD_OUT);
        (await ListedAsync(productId)).Should().BeFalse();
    }

    /// <summary>Places an order for the product through the API.</summary>
    /// <param name="client">The signed-in client.</param>
    /// <param name="productId">The product to order.</param>
    /// <param name="lines">How many lines of it the order carries.</param>
    /// <returns>The API's response.</returns>
    private static Task<HttpResponseMessage> OrderAsync(HttpClient client, int productId, int lines = 1)
    {
        return client.PostAsJsonAsync("/api/orders", new
        {
            paymentMethod = "bank_transfer",
            items = Enumerable.Range(0, lines)
                .Select(_ => new { productId, billingCycle = nameof(BillingCycle.Monthly) })
                .ToArray(),
        });
    }

    /// <summary>Whether the anonymous public catalogue lists the product.</summary>
    /// <param name="productId">The product to look for.</param>
    /// <returns><see langword="true"/> when it is listed.</returns>
    private async Task<bool> ListedAsync(int productId)
    {
        using var body = JsonDocument.Parse(
            await factory.CreateClient().GetStringAsync($"/api/public/products?productId={productId}&currency=USD"));
        return body.RootElement.EnumerateArray().Any(p => p.GetProperty("id").GetInt32() == productId);
    }

    /// <summary>Seeds an active USD product with the given limits.</summary>
    /// <param name="max">The cap on live services, or null for unlimited.</param>
    /// <param name="onePerClient">Whether a client may hold only one.</param>
    /// <returns>The product id.</returns>
    private async Task<int> SeedLimitedProductAsync(int? max, bool onePerClient)
    {
        await OrdersTestSeeds.EnableBankTransferAsync(factory);
        var productId = await OrdersTestSeeds.SeedProductAsync(factory);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Products.FindAsync(productId);
        product!.SetMaxActiveServices(max);
        product.SetOnePerClient(onePerClient);
        await db.SaveChangesAsync();
        return productId;
    }
}
