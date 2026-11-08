using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Settings;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Orders;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <c>POST /api/orders/{id}/items/{itemId}/retry</c> against Postgres: a failed line is run again and
/// fulfilled once its cause is gone, a line that is already fulfilled is refused and never fulfilled twice,
/// a domain the client already holds is not registered twice, two concurrent retries run one fulfilment,
/// every retry is audited, and the authorization boundary (anonymous, wrong role) holds.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class RetryOrderLineTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Retrying a line of an order that does not exist answers 404 with <c>ORDER_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task MissingOrderRefusesNotFound()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync("/api/orders/987654321/items/1/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_NOT_FOUND);
    }

    /// <summary>A line the order does not hold answers 404 with <c>ORDER_ITEM_NOT_FOUND</c>.</summary>
    [Fact]
    public async Task MissingLineRefusesNotFound()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();

        var response = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/987654321/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_ITEM_NOT_FOUND);
    }

    /// <summary>A line of another order is not found through this order, even though the line exists.</summary>
    [Fact]
    public async Task ALineOfAnotherOrderRefusesNotFound()
    {
        var first = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var second = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{first.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/orders/{second.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var foreignLine = (await ReadItemsAsync(second.OrderId)).First().Id;

        var response = await admin.PostAsync($"/api/orders/{first.OrderId}/items/{foreignLine}/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_ITEM_NOT_FOUND);
    }

    /// <summary>A non-positive id is a validation error.</summary>
    [Fact]
    public async Task NonPositiveIdsAreRefused()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync("/api/orders/1/items/0/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A line of an order that was never accepted is not retryable: <c>ORDER_LINE_NOT_RETRYABLE</c>, 409.</summary>
    [Fact]
    public async Task ALineOfAPendingOrderIsNotRetryable()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        var line = (await ReadItemsAsync(seeded.OrderId)).First();

        var response = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{line.Id}/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_LINE_NOT_RETRYABLE);
    }

    /// <summary>
    /// While the registrar still refuses the domain, the retry runs the fulfilment, the line fails again with
    /// the code, one more attempt is counted, the order stays Incomplete and the hosting line is not created twice.
    /// </summary>
    [Fact]
    public async Task RetryFailsAgainWhileTheDomainIsStillTaken()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var domainLine = (await ReadItemsAsync(seeded.OrderId)).First(i => i.Domain == seeded.Domain);

        var response = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{domainLine.Id}/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var outcome = await response.Content.ReadFromJsonAsync<JsonElement>();
        outcome.GetProperty("failedLines").GetArrayLength().Should().Be(1);
        var after = (await ReadItemsAsync(seeded.OrderId)).Single(i => i.Id == domainLine.Id);
        after.FulfilmentState.Should().Be(OrderItemFulfilmentState.Failed);
        after.FulfilmentErrorCode.Should().Be(HostpanelErrorCodes.DOMAIN_ALREADY_REGISTERED);
        after.FulfilmentAttempts.Should().Be(2);
        (await ReadOrderAsync(seeded.OrderId)).Status.Should().Be(OrderStatus.Incomplete);
        (await CountServicesAsync(seeded.ClientId, seeded.ProductId)).Should().Be(1);
    }

    /// <summary>
    /// Once the cause is gone the retry registers the domain, the line is Fulfilled with no code, the order
    /// returns to Active, and the line that was already fulfilled is left alone.
    /// </summary>
    [Fact]
    public async Task RetryFulfilsTheLineOnceTheDomainIsFree()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var domainLine = (await ReadItemsAsync(seeded.OrderId)).First(i => i.Domain == seeded.Domain);
        await ReleaseDomainAsync(seeded.Domain);

        var response = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{domainLine.Id}/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("failedLines").GetArrayLength().Should().Be(0);
        var items = await ReadItemsAsync(seeded.OrderId);
        var after = items.Single(i => i.Id == domainLine.Id);
        after.FulfilmentState.Should().Be(OrderItemFulfilmentState.Fulfilled);
        after.FulfilmentErrorCode.Should().BeNull();
        after.FulfilmentAttempts.Should().Be(2);
        items.Where(i => i.Id != domainLine.Id).Should().OnlyContain(i => i.FulfilmentState == OrderItemFulfilmentState.Fulfilled && i.FulfilmentAttempts == 1);
        (await ReadOrderAsync(seeded.OrderId)).Status.Should().Be(OrderStatus.Active);
        (await CountDomainsAsync(seeded.Domain, seeded.ClientId)).Should().Be(1);
        (await CountServicesAsync(seeded.ClientId, seeded.ProductId)).Should().Be(1);
    }

    /// <summary>
    /// A line that was fulfilled by a retry is refused when retried again (<c>ORDER_LINE_ALREADY_FULFILLED</c>, 409),
    /// and neither the domain nor the service is created a second time.
    /// </summary>
    [Fact]
    public async Task ARetryOfAFulfilledLineIsRefusedAndFulfilsNothingTwice()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var items = await ReadItemsAsync(seeded.OrderId);
        var domainLine = items.First(i => i.Domain == seeded.Domain);
        var hostingLine = items.First(i => i.Domain is null);
        await ReleaseDomainAsync(seeded.Domain);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{domainLine.Id}/retry", content: null)).EnsureSuccessStatusCode();

        var again = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{domainLine.Id}/retry", content: null);
        var hosting = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{hostingLine.Id}/retry", content: null);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await OrdersTestSeeds.AssertCodeAsync(again, HostpanelErrorCodes.ORDER_LINE_ALREADY_FULFILLED);
        hosting.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await OrdersTestSeeds.AssertCodeAsync(hosting, HostpanelErrorCodes.ORDER_LINE_ALREADY_FULFILLED);
        (await ReadItemsAsync(seeded.OrderId)).Single(i => i.Id == domainLine.Id).FulfilmentAttempts.Should().Be(2);
        (await CountDomainsAsync(seeded.Domain, seeded.ClientId)).Should().Be(1);
        (await CountServicesAsync(seeded.ClientId, seeded.ProductId)).Should().Be(1);
    }

    /// <summary>
    /// A first attempt that registered the domain but died before recording it leaves the line Failed with the
    /// domain already held by the client: the retry counts it as done, so the same domain is never registered twice.
    /// </summary>
    [Fact]
    public async Task ADomainTheClientAlreadyHoldsIsNotRegisteredTwice()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var domainLine = (await ReadItemsAsync(seeded.OrderId)).First(i => i.Domain == seeded.Domain);
        await ReleaseDomainAsync(seeded.Domain);
        var heldId = await HoldDomainAsync(seeded.ClientId, seeded.Domain);

        var response = await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{domainLine.Id}/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("failedLines").GetArrayLength().Should().Be(0);
        (await ReadItemsAsync(seeded.OrderId)).Single(i => i.Id == domainLine.Id).FulfilmentState.Should().Be(OrderItemFulfilmentState.Fulfilled);
        (await ReadOrderAsync(seeded.OrderId)).Status.Should().Be(OrderStatus.Active);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var domains = await db.Domains.AsNoTracking().Where(d => d.Name == RegisteredDomain.NormaliseName(seeded.Domain)).ToListAsync();
        domains.Should().ContainSingle().Which.Id.Should().Be(heldId);
    }

    /// <summary>
    /// Two retries of the same failed line sent at once run one fulfilment: one answers 200, the other is refused
    /// with a 409, and the domain is registered once.
    /// </summary>
    [Fact]
    public async Task TwoConcurrentRetriesRunTheFulfilmentOnce()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var domainLine = (await ReadItemsAsync(seeded.OrderId)).First(i => i.Domain == seeded.Domain);
        await ReleaseDomainAsync(seeded.Domain);
        var url = $"/api/orders/{seeded.OrderId}/items/{domainLine.Id}/retry";

        var responses = await Task.WhenAll(admin.PostAsync(url, content: null), admin.PostAsync(url, content: null));

        responses.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
        (await CountDomainsAsync(seeded.Domain, seeded.ClientId)).Should().Be(1);
        var after = (await ReadItemsAsync(seeded.OrderId)).Single(i => i.Id == domainLine.Id);
        after.FulfilmentState.Should().Be(OrderItemFulfilmentState.Fulfilled);
        after.FulfilmentAttempts.Should().Be(2);
    }

    /// <summary>
    /// How long a retry's claim is respected is the <c>Orders:RetryLeaseMinutes</c> option: a claim two minutes old holds the
    /// line under the default lease of ten minutes (409), and is taken over once the configured lease is shorter than that.
    /// </summary>
    [Fact]
    public async Task ARetryClaimIsRespectedForTheConfiguredLease()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var domainLine = (await ReadItemsAsync(seeded.OrderId)).First(i => i.Domain == seeded.Domain);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var claimedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
            await db.OrderItems.Where(i => i.Id == domainLine.Id).ExecuteUpdateAsync(s => s
                .SetProperty(i => i.FulfilmentState, OrderItemFulfilmentState.InProgress)
                .SetProperty(i => i.LastFulfilmentAttemptAt, claimedAt));
        }

        var url = $"/api/orders/{seeded.OrderId}/items/{domainLine.Id}/retry";
        var held = await admin.PostAsync(url, content: null);
        using var shortLease = factory.WithWebHostBuilder(b => b.UseSetting(
            $"{OrderOptions.SectionName}:{nameof(OrderOptions.RetryLeaseMinutes)}", "1"));
        var taken = await (await OrdersTestReads.AdminOfAsync(shortLease, factory)).PostAsync(url, content: null);

        held.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await OrdersTestSeeds.AssertCodeAsync(held, HostpanelErrorCodes.ORDER_LINE_NOT_RETRYABLE);
        taken.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadItemsAsync(seeded.OrderId)).Single(i => i.Id == domainLine.Id).FulfilmentAttempts.Should().Be(2);
    }

    /// <summary>Every retry is written to the client's activity log, naming the order and the line.</summary>
    [Fact]
    public async Task ARetryIsAudited()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var domainLine = (await ReadItemsAsync(seeded.OrderId)).First(i => i.Domain == seeded.Domain);

        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/items/{domainLine.Id}/retry", content: null)).EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logs = await db.ActivityLogs.AsNoTracking().Where(l => l.ClientId == seeded.ClientId).ToListAsync();
        logs.Should().ContainSingle(l => l.Description.Contains($"Retried Order Line - Order ID: {seeded.OrderId} - Line ID: {domainLine.Id}"));
    }

    /// <summary>Retrying is not for anonymous callers.</summary>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().PostAsync("/api/orders/1/items/1/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client caller, who is not an admin, is refused with 403.</summary>
    [Fact]
    public async Task RefusesAClientCaller()
    {
        var client = await OrdersTestSeeds.ClientCallerAsync(factory);

        var response = await client.PostAsync("/api/orders/1/items/1/retry", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>Reads an order's lines from the database, with their fulfilment records.</summary>
    /// <param name="orderId">The order.</param>
    /// <returns>The lines in id order.</returns>
    private async Task<List<OrderItem>> ReadItemsAsync(int orderId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().Include(o => o.Items).SingleAsync(o => o.Id == orderId);
        return order.Items.OrderBy(i => i.Id).ToList();
    }

    /// <summary>Reads an order from the database.</summary>
    /// <param name="orderId">The order.</param>
    /// <returns>The order.</returns>
    private async Task<Order> ReadOrderAsync(int orderId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
    }

    /// <summary>Removes the domain another client held, which is what made the registrar refuse the registration.</summary>
    /// <param name="domain">The domain name.</param>
    /// <returns>A task that completes once the domain is gone.</returns>
    private async Task ReleaseDomainAsync(string domain)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var name = RegisteredDomain.NormaliseName(domain);
        await db.Domains.Where(d => d.Name == name).ExecuteDeleteAsync();
    }

    /// <summary>Registers the domain to the ordering client, as a first attempt that died before recording it would have.</summary>
    /// <param name="clientId">The ordering client.</param>
    /// <param name="domain">The domain name.</param>
    /// <returns>The held domain's id.</returns>
    private async Task<int> HoldDomainAsync(int clientId, string domain)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var held = RegisteredDomain.Register(
            clientId, domain, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddYears(1), autoRenew: false, whoisPrivacy: false, priceCurrency: "USD");
        db.Domains.Add(held);
        await db.SaveChangesAsync();
        return held.Id;
    }

    /// <summary>Counts the domains a client holds under a name.</summary>
    /// <param name="domain">The domain name.</param>
    /// <param name="clientId">The client.</param>
    /// <returns>How many rows there are.</returns>
    private async Task<int> CountDomainsAsync(string domain, int clientId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var name = RegisteredDomain.NormaliseName(domain);
        return await db.Domains.AsNoTracking().CountAsync(d => d.Name == name && d.ClientId == clientId);
    }

    /// <summary>Counts the services a client has of a product.</summary>
    /// <param name="clientId">The client.</param>
    /// <param name="productId">The product.</param>
    /// <returns>How many rows there are.</returns>
    private async Task<int> CountServicesAsync(int clientId, int productId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.ClientServices.AsNoTracking().CountAsync(s => s.ClientId == clientId && s.ProductId == productId);
    }
}
