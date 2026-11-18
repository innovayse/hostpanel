using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Products.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Innovayse.Hostpanel.Integration.Tests.Shared;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

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
        var productId = await OrdersTestSeeds.SeedLimitedProductAsync(factory, max: null, onePerClient: true);
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
        var productId = await OrdersTestSeeds.SeedLimitedProductAsync(factory, max: null, onePerClient: true);
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
        var productId = await OrdersTestSeeds.SeedLimitedProductAsync(factory, max: 1, onePerClient: false);
        var (first, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "capped-a");
        var (second, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "capped-b");

        (await ListedAsync(productId)).Should().BeTrue();
        (await OrderAsync(first, productId)).StatusCode.Should().Be(HttpStatusCode.Created);

        var refused = await OrderAsync(second, productId);
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await OrdersTestSeeds.AssertCodeAsync(refused, HostpanelErrorCodes.ORDER_PRODUCT_SOLD_OUT);
        (await ListedAsync(productId)).Should().BeFalse();
    }

    /// <summary>
    /// Cancelling an order that holds the last place gives the place back: the product is listed again and another
    /// client can order it.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PlaceOrder_CancelledOrder_FreesItsPlaceAsync()
    {
        var productId = await OrdersTestSeeds.SeedLimitedProductAsync(factory, max: 1, onePerClient: false);
        var (first, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "freed-a");
        var (second, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "freed-b");
        var placed = await OrderAsync(first, productId);
        placed.StatusCode.Should().Be(HttpStatusCode.Created);
        var orderId = (await placed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderId").GetInt32();
        (await OrderAsync(second, productId)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{orderId}/cancel", content: null)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ListedAsync(productId)).Should().BeTrue();
        (await OrderAsync(second, productId)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    /// <summary>
    /// More orders than a capped product has places, placed at the same moment by different clients, take exactly the
    /// places there are: the count and the save run under the product's lock, so none passes on a stale count.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PlaceOrder_ParallelOrdersAgainstACap_TakeExactlyTheCapAsync()
    {
        const int Parallel = 6;
        const int Cap = Parallel - 1;
        var productId = await OrdersTestSeeds.SeedLimitedProductAsync(factory, max: Cap, onePerClient: false);
        var clients = new List<HttpClient>();
        for (var i = 0; i < Parallel; i++)
        {
            clients.Add((await ClientsTestSeeds.RegisterClientAsync(factory, $"parallel-cap-{i}")).Client);
        }

        var responses = await Task.WhenAll(clients.Select(client => OrderAsync(client, productId)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(Cap);
        var refused = responses.Where(r => r.StatusCode != HttpStatusCode.Created).ToList();
        refused.Should().ContainSingle().Which.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await OrdersTestSeeds.AssertCodeAsync(refused[0], HostpanelErrorCodes.ORDER_PRODUCT_SOLD_OUT);
    }

    /// <summary>
    /// Two orders of one one-per-client product placed at the same moment by the same client: exactly one is placed.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PlaceOrder_ParallelOrdersOfOneClient_PlaceExactlyOneAsync()
    {
        var productId = await OrdersTestSeeds.SeedLimitedProductAsync(factory, max: null, onePerClient: true);
        var (client, _, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "parallel-one");

        // A first order grants the caller the Client role; two first orders placed together race to grant it, which is
        // not what this test is about, so the caller already holds it.
        (await OrderAsync(client, await OrdersTestSeeds.SeedProductAsync(factory))).StatusCode.Should().Be(HttpStatusCode.Created);

        var responses = await Task.WhenAll(OrderAsync(client, productId), OrderAsync(client, productId));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        var refused = responses.Single(r => r.StatusCode != HttpStatusCode.Created);
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await OrdersTestSeeds.AssertCodeAsync(refused, HostpanelErrorCodes.ORDER_PRODUCT_ONE_PER_CLIENT);
    }

    /// <summary>
    /// When the order's own save fails inside the product places transaction, no order is left and the invoice raised
    /// for it is cancelled, so the client holds nothing payable; the failure the caller sees is the database's own
    /// refusal, not an error from the clean-up.
    /// </summary>
    /// <remarks>
    /// The failure is a real database refusal: a trigger on the order lines table refuses the one line naming this
    /// test's product, and is dropped again afterwards.
    /// </remarks>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PlaceOrder_OrderSaveFailsInThePlacesTransaction_CancelsTheInvoiceAndSurfacesTheOriginalErrorAsync()
    {
        var productId = await OrdersTestSeeds.SeedLimitedProductAsync(factory, max: 5, onePerClient: false);
        var (client, clientId, _) = await ClientsTestSeeds.RegisterClientAsync(factory, "save-fails");
        var logs = new RecordingLoggerProvider();
        using var host = factory.WithWebHostBuilder(
            builder => builder.ConfigureTestServices(services => services.AddSingleton<ILoggerProvider>(logs)));
        using var http = host.CreateClient();
        http.DefaultRequestHeaders.Authorization = client.DefaultRequestHeaders.Authorization;
        await ExecuteAsync(
            "CREATE OR REPLACE FUNCTION \"RefuseLimitedOrderItem\"() RETURNS trigger AS $$ BEGIN " +
            $"IF NEW.\"ProductId\" = {productId} THEN RAISE EXCEPTION 'refused by test'; END IF; RETURN NEW; END $$ LANGUAGE plpgsql;");
        await ExecuteAsync(
            "CREATE TRIGGER \"RefuseLimitedOrderItem\" BEFORE INSERT ON \"OrderItems\" " +
            "FOR EACH ROW EXECUTE FUNCTION \"RefuseLimitedOrderItem\"();");

        try
        {
            (await OrderAsync(http, productId)).StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        }
        finally
        {
            await ExecuteAsync("DROP TRIGGER IF EXISTS \"RefuseLimitedOrderItem\" ON \"OrderItems\";");
            await ExecuteAsync("DROP FUNCTION IF EXISTS \"RefuseLimitedOrderItem\"();");
        }

        var refusals = logs.Entries
            .Where(e => e.Level == LogLevel.Error && e.Exception is not null)
            .SelectMany(e => Chain(e.Exception!))
            .OfType<PostgresException>()
            .ToList();
        refusals.Should().Contain(e => e.MessageText == "refused by test");
        refusals.Should().NotContain(e => e.SqlState == PostgresErrorCodes.InFailedSqlTransaction);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Orders.AsNoTracking().AnyAsync(o => o.ClientId == clientId)).Should().BeFalse();
        (await db.Invoices.AsNoTracking().Where(i => i.ClientId == clientId).Select(i => i.Status).ToListAsync())
            .Should().ContainSingle().Which.Should().Be(InvoiceStatus.Cancelled);
    }

    /// <summary>Runs one statement against the test database.</summary>
    /// <param name="sql">The statement to run.</param>
    /// <returns>A task that completes when the statement has run.</returns>
    private async Task ExecuteAsync(string sql)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync(sql);
    }

    /// <summary>Walks an exception and everything it wraps.</summary>
    /// <param name="exception">The outermost exception.</param>
    /// <returns>The exception, then each inner exception in turn.</returns>
    private static IEnumerable<Exception> Chain(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
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
}
