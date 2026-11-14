using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Orders.Commands.BeginOrderLineRefund;
using Innovayse.Hostpanel.Application.Orders.Commands.ReleaseOrderLineRefund;
using Innovayse.Hostpanel.Application.Orders.Common.Enums;
using Innovayse.Hostpanel.Domain.Orders.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Innovayse.Hostpanel.Integration.Tests.Orders;

/// <summary>
/// <c>GET /api/orders</c> against Postgres: the happy path, the search on the order number and id, the status filter
/// (an unknown status is refused, never read as no filter), the paging window and the authorization boundary
/// (anonymous, wrong role).
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

    /// <summary>The search matches the order number case-insensitively as a substring and leaves other orders out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchFindsTheOrderNumberInAnyLetterCase()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        var mix = await OrdersTestSeeds.SeedOrderMixAsync(factory);

        var (ids, totalCount) = await OrdersTestSeeds.ListOrderIdsAsync(
            admin, $"/api/orders?page=1&pageSize=100&search={mix.Token.ToUpperInvariant()}-gamma");
        var (everything, everythingCount) = await OrdersTestSeeds.ListOrderIdsAsync(
            admin, $"/api/orders?page=1&pageSize=100&search={mix.Token}");

        ids.Should().Equal(mix.Gamma);
        totalCount.Should().Be(1);
        everything.Should().Equal(mix.Delta, mix.Gamma, mix.Beta, mix.Alpha);
        everythingCount.Should().Be(4);
    }

    /// <summary>A whole number typed into the search finds the order with that id.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchFindsTheOrderById()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        var mix = await OrdersTestSeeds.SeedOrderMixAsync(factory);

        var (ids, _) = await OrdersTestSeeds.ListOrderIdsAsync(admin, $"/api/orders?page=1&pageSize=100&search={mix.Beta}");

        ids.Should().Contain(mix.Beta);
    }

    /// <summary>A percent sign and an underscore typed into the search match themselves, not any text.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchWildcardsAreLiteral()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        var token = Guid.NewGuid().ToString("N")[..8];
        var ids = await OrdersTestSeeds.SeedOrdersAsync(
            factory,
            [
                ($"{token}-50%", OrderStatus.Pending),
                ($"{token}-50X", OrderStatus.Pending),
                ($"{token}-A_C", OrderStatus.Pending),
                ($"{token}-ABC", OrderStatus.Pending),
            ]);

        var (percent, _) = await OrdersTestSeeds.ListOrderIdsAsync(admin, $"/api/orders?page=1&pageSize=100&search={token}-50%25");
        var (underscore, _) = await OrdersTestSeeds.ListOrderIdsAsync(admin, $"/api/orders?page=1&pageSize=100&search={token}-A_C");
        var (bareWildcard, bareCount) = await OrdersTestSeeds.ListOrderIdsAsync(admin, $"/api/orders?page=1&pageSize=100&search={token}%25%25");

        percent.Should().Equal(ids[0]);
        underscore.Should().Equal(ids[2]);
        bareWildcard.Should().BeEmpty();
        bareCount.Should().Be(0);
    }

    /// <summary>The status filter, scoped to one mix by the search, keeps only the orders in that status, newest first.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StatusFilterNarrowsWithinTheSearch()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        var mix = await OrdersTestSeeds.SeedOrderMixAsync(factory);
        var scope = $"/api/orders?page=1&pageSize=100&search={mix.Token}";

        var (pending, pendingCount) = await OrdersTestSeeds.ListOrderIdsAsync(admin, $"{scope}&status=Pending");
        var (cancelled, _) = await OrdersTestSeeds.ListOrderIdsAsync(admin, $"{scope}&status=Cancelled");
        var (active, _) = await OrdersTestSeeds.ListOrderIdsAsync(admin, $"{scope}&status=Active");
        var (fraud, fraudCount) = await OrdersTestSeeds.ListOrderIdsAsync(admin, $"{scope}&status=Fraud");

        pending.Should().Equal(mix.Delta, mix.Alpha);
        pendingCount.Should().Be(2);
        cancelled.Should().Equal(mix.Beta);
        active.Should().Equal(mix.Gamma);
        fraud.Should().BeEmpty();
        fraudCount.Should().Be(0);
    }

    /// <summary>The search and the status narrow together: an order must pass both.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchAndStatusApplyTogether()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        var mix = await OrdersTestSeeds.SeedOrderMixAsync(factory);

        var (match, matchCount) = await OrdersTestSeeds.ListOrderIdsAsync(
            admin, $"/api/orders?page=1&pageSize=100&search={mix.Token}-alpha&status=Pending");
        var (mismatch, mismatchCount) = await OrdersTestSeeds.ListOrderIdsAsync(
            admin, $"/api/orders?page=1&pageSize=100&search={mix.Token}-alpha&status=Cancelled");

        match.Should().Equal(mix.Alpha);
        matchCount.Should().Be(1);
        mismatch.Should().BeEmpty();
        mismatchCount.Should().Be(0);
    }

    /// <summary>A filter is applied before paging: the total counts every match and each page holds its slice, newest first.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FilterIsAppliedBeforePaging()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);
        var mix = await OrdersTestSeeds.SeedOrderMixAsync(factory);
        var scope = $"/api/orders?pageSize=3&search={mix.Token}";

        var (first, firstCount) = await OrdersTestSeeds.ListOrderIdsAsync(admin, $"{scope}&page=1");
        var (second, secondCount) = await OrdersTestSeeds.ListOrderIdsAsync(admin, $"{scope}&page=2");
        var (beyond, _) = await OrdersTestSeeds.ListOrderIdsAsync(admin, $"{scope}&page=3");
        var (pendingPage, pendingCount) = await OrdersTestSeeds.ListOrderIdsAsync(admin, $"{scope}&page=1&status=Pending");

        first.Should().Equal(mix.Delta, mix.Gamma, mix.Beta);
        firstCount.Should().Be(4);
        second.Should().Equal(mix.Alpha);
        secondCount.Should().Be(4);
        beyond.Should().BeEmpty();
        pendingPage.Should().Equal(mix.Delta, mix.Alpha);
        pendingCount.Should().Be(2);
    }

    /// <summary>A page below one or a page size outside the limit is refused with 400, not corrected.</summary>
    /// <param name="queryString">The paging part of the query string.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("page=0&pageSize=20")]
    [InlineData("pageSize=20")]
    [InlineData("page=1&pageSize=0")]
    [InlineData("page=1&pageSize=101")]
    public async Task PagingOutsideTheLimitsIsRefused(string queryString)
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/orders?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await OrdersTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>A search longer than the limit is refused with 400, not cut.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchOverTheLimitIsRefused()
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/orders?page=1&pageSize=20&search={new string('a', PagingLimits.MaxSearchLength + 1)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await OrdersTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>A status that names nothing, by name or by number, is refused with 400 rather than listing every order.</summary>
    /// <param name="filter">The filter's key and value.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("status=NoSuchStatus")]
    [InlineData("status=99")]
    public async Task UnknownStatusIsRefused(string filter)
    {
        var admin = await OrdersTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/orders?page=1&pageSize=100&{filter}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await OrdersTestSeeds.AssertCodeAsync(response, SharedErrorCodes.ValidationError);
    }

    /// <summary>Listing orders is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/orders?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A plain client caller, who is not an admin, is refused with 403.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RefusesAClientCaller()
    {
        var client = await OrdersTestSeeds.ClientCallerAsync(factory);

        var response = await client.GetAsync("/api/orders?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
