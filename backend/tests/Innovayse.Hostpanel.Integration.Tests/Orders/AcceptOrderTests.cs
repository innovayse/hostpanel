using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Orders.Common.Enums;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <c>POST /api/orders/{id}/accept</c> against Postgres: the happy path, the missing-order
/// refusal, per-line failure reports (a rejected domain beside a hosting line),
/// and the authorization boundary (anonymous, wrong role).
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class AcceptOrderTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An id nothing names answers 404 with <c>ORDER_NOT_FOUND</c>.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task MissingOrderRefusesNotFound()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        const int MissingId = 987_654_321;

        var response = await admin.PostAsync($"/api/orders/{MissingId}/accept", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await OrdersTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.ORDER_NOT_FOUND);
    }

    /// <summary>Accepting a pending order moves it to Active and reports no failed line.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AcceptMovesAPendingOrderToActive()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/orders/{orderId}/accept", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var outcome = await response.Content.ReadFromJsonAsync<JsonElement>();
        outcome.GetProperty("failedLines").GetArrayLength().Should().Be(0);
        var read = await admin.GetFromJsonAsync<JsonElement>($"/api/orders/{orderId}");
        read.GetProperty("status").GetString().Should().Be(nameof(OrderStatus.Active));
    }

    /// <summary>
    /// A domain line is fulfilled as a domain, never as a client service: a registration the
    /// registrar refuses is reported as a failed domain line, and the hosting line after it is
    /// still fulfilled, so one failure never stops the rest.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AcceptRunsTheDomainActionAndKeepsGoingPastAFailedLine()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var outcome = await response.Content.ReadFromJsonAsync<JsonElement>();
        var failed = outcome.GetProperty("failedLines");
        failed.GetArrayLength().Should().Be(1);
        failed[0].GetProperty("kind").GetString().Should().Be(nameof(OrderLineKind.Domain));
        failed[0].GetProperty("domain").GetString().Should().Be(seeded.Domain);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var services = await db.ClientServices.AsNoTracking().Where(s => s.ClientId == seeded.ClientId).ToListAsync();
        services.Should().ContainSingle(s => s.ProductId == seeded.ProductId);
        var order = await db.Orders.AsNoTracking().Include(o => o.Items).SingleAsync(o => o.Id == seeded.OrderId);
        order.Status.Should().Be(OrderStatus.Incomplete);
        var domainLine = order.Items.Single(i => i.Domain == seeded.Domain);
        domainLine.FulfilmentState.Should().Be(OrderItemFulfilmentState.Failed);
        domainLine.FulfilmentErrorCode.Should().Be(HostpanelErrorCodes.DOMAIN_ALREADY_REGISTERED);
        domainLine.FulfilmentAttempts.Should().Be(1);
        domainLine.LastFulfilmentAttemptAt.Should().NotBeNull();
        var hostingLine = order.Items.Single(i => i.Domain is null);
        hostingLine.FulfilmentState.Should().Be(OrderItemFulfilmentState.Fulfilled);
        hostingLine.FulfilmentErrorCode.Should().BeNull();
        hostingLine.FulfilmentAttempts.Should().Be(1);
    }

    /// <summary>
    /// When every line is fulfilled the order stays Active, each line is Fulfilled after one attempt, and the
    /// order reads back with the state of its lines and no failed line.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AcceptOfAnOrderWhoseLinesAllSucceedLeavesItActiveWithFulfilledLines()
    {
        var seeded = await OrdersTestSeeds.SeedDigitalAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var read = await admin.GetFromJsonAsync<JsonElement>($"/api/orders/{seeded.OrderId}");
        read.GetProperty("status").GetString().Should().Be(nameof(OrderStatus.Active));
        var items = read.GetProperty("items").EnumerateArray().ToList();
        items.Should().HaveCount(2);
        items.Should().OnlyContain(i => i.GetProperty("fulfilmentState").GetString() == nameof(OrderItemFulfilmentState.Fulfilled));
        items.Should().OnlyContain(i => i.GetProperty("fulfilmentAttempts").GetInt32() == 1);
        items.Should().OnlyContain(i => i.GetProperty("fulfilmentErrorCode").ValueKind == JsonValueKind.Null);
    }

    /// <summary>Accepting is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory);

        var response = await factory.CreateClient().PostAsync($"/api/orders/{orderId}/accept", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client caller, who is not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAClientCaller()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory);
        var client = await OrdersTestSeeds.ClientCallerAsync(factory);

        var response = await client.PostAsync($"/api/orders/{orderId}/accept", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
