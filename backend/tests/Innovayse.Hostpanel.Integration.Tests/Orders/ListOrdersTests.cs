using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Orders.Commands.BeginOrderLineRefund;
using Innovayse.Hostpanel.Application.Orders.Commands.ReleaseOrderLineRefund;
using Innovayse.Hostpanel.Application.Orders.Common.Enums;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <c>GET /api/orders</c> against Postgres: the happy path, the status filter (an unknown status
/// is refused, never read as no filter) and the authorization boundary (anonymous, wrong role).
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListOrdersTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A seeded pending order appears in the admin list, named after its client.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListShowsTheSeededOrder()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var list = await admin.GetFromJsonAsync<JsonElement>("/api/orders?page=1&pageSize=100");

        list.GetProperty("items").EnumerateArray().Should().Contain(o => o.GetProperty("id").GetInt32() == orderId);
        list.GetProperty("items").EnumerateArray()
            .Single(o => o.GetProperty("id").GetInt32() == orderId)
            .GetProperty("clientName").GetString().Should().Be("Ada Lovelace");
    }

    /// <summary>The list says which currency each order's total is in, so the admin can show a unit.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListCarriesTheOrdersCurrency()
    {
        var orderId = await OrdersTestSeeds.SeedOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var list = await admin.GetFromJsonAsync<JsonElement>("/api/orders?page=1&pageSize=100");

        list.GetProperty("items").EnumerateArray()
            .Single(o => o.GetProperty("id").GetInt32() == orderId)
            .GetProperty("currency").GetString().Should().Be("USD");
    }

    /// <summary>The status filter keeps only orders in that status.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StatusFilterKeepsOnlyThatStatus()
    {
        var pendingId = await OrdersTestSeeds.SeedOrderAsync(factory);
        var cancelledId = await OrdersTestSeeds.SeedOrderAsync(factory, OrderStatus.Cancelled);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var list = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/orders?page=1&pageSize=100&status={nameof(OrderStatus.Cancelled)}");

        var items = list.GetProperty("items").EnumerateArray().ToList();
        items.Should().NotContain(o => o.GetProperty("id").GetInt32() == pendingId);
        items.Should().Contain(o => o.GetProperty("id").GetInt32() == cancelledId);
        items.Should().OnlyContain(o => o.GetProperty("status").GetString() == nameof(OrderStatus.Cancelled));
    }

    /// <summary>
    /// An accepted order with a failed line is listed under the Incomplete filter with the count of its failed
    /// lines, and an order whose lines all succeeded is not.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task IncompleteFilterListsTheOrdersWithAFailedLine()
    {
        var failing = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var clean = await OrdersTestSeeds.SeedDigitalAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{failing.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        (await admin.PostAsync($"/api/orders/{clean.OrderId}/accept", content: null)).EnsureSuccessStatusCode();

        var list = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/orders?page=1&pageSize=100&status={nameof(OrderStatus.Incomplete)}");

        var items = list.GetProperty("items").EnumerateArray().ToList();
        items.Should().NotContain(o => o.GetProperty("id").GetInt32() == clean.OrderId);
        items.Single(o => o.GetProperty("id").GetInt32() == failing.OrderId).GetProperty("failedLineCount").GetInt32().Should().Be(1);
        items.Should().OnlyContain(o => o.GetProperty("status").GetString() == nameof(OrderStatus.Incomplete));
    }

    /// <summary>
    /// A partly refunded line still needs staff to finish its refund by hand, so it is counted with the failed lines and its
    /// order is listed as Incomplete.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task APartlyRefundedLineIsCountedWithTheLinesThatNeedStaff()
    {
        var seeded = await OrdersTestSeeds.SeedTakenDomainAndHostingOrderAsync(factory);
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        (await admin.PostAsync($"/api/orders/{seeded.OrderId}/accept", content: null)).EnsureSuccessStatusCode();
        var domainLine = (await OrdersTestReads.ReadItemsAsync(factory.Services, seeded.OrderId)).Single(i => i.Domain == seeded.Domain);
        using (var scope = factory.Services.CreateScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            (await bus.InvokeAsync<Result>(new BeginOrderLineRefundCommand(seeded.OrderId, domainLine.Id))).IsSuccess.Should().BeTrue();
            (await bus.InvokeAsync<Result>(new ReleaseOrderLineRefundCommand(seeded.OrderId, domainLine.Id, OrderLineRefundOutcome.PartlyRefunded)))
                .IsSuccess.Should().BeTrue();
        }

        var list = await admin.GetFromJsonAsync<JsonElement>(
            $"/api/orders?page=1&pageSize=100&status={nameof(OrderStatus.Incomplete)}");

        var item = list.GetProperty("items").EnumerateArray().Single(o => o.GetProperty("id").GetInt32() == seeded.OrderId);
        item.GetProperty("failedLineCount").GetInt32().Should().Be(1);
    }

    /// <summary>An unknown status is refused with 400 rather than silently listing every order.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownStatusIsRefused()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/orders?page=1&pageSize=100&status=NoSuchStatus");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Listing orders is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/orders");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client caller, who is not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAClientCaller()
    {
        var client = await OrdersTestSeeds.ClientCallerAsync(factory);

        var response = await client.GetAsync("/api/orders");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
