using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Billing.Queries.ListClientBillableItems;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Domain.Billing.Enums;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/clients/{clientId}/billable-items</c> against Postgres: a client's own uninvoiced
/// and invoiced billable items, both narrowed by the search and the type filter, the invoiced ones paged.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListClientBillableItemsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A newly created client has no billable items yet.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListForAFreshClientIsEmpty()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients/{clientId}/billable-items?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("uninvoicedItems").GetArrayLength().Should().Be(0);
        body.RootElement.GetProperty("uninvoicedTotal").GetArrayLength().Should().Be(0);
    }

    /// <summary>Uninvoiced items in two currencies give one total per currency, never one mixed sum.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListTotalsUninvoicedItemsPerCurrency()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        foreach (var (amount, currency) in new[] { (10m, "USD"), (5m, "USD"), (5000m, "AMD") })
        {
            var created = await admin.PostAsJsonAsync($"/api/clients/{clientId}/billable-items", new
            {
                description = "Charge",
                amount,
                currency,
                type = nameof(BillableItemType.OneTime),
            });
            created.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var response = await admin.GetAsync($"/api/clients/{clientId}/billable-items?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var totals = body.RootElement.GetProperty("uninvoicedTotal").EnumerateArray()
            .Select(t => (Currency: t.GetProperty("currency").GetString(), Amount: t.GetProperty("amount").GetDecimal()))
            .ToList();
        totals.Should().Equal((Currency: "AMD", Amount: 5000m), (Currency: "USD", Amount: 15m));
    }

    /// <summary>Items created as <c>usd</c> and <c>USD</c> are one currency and give one total.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListGivesOneTotalForTheSameCurrencyInAnyCase()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        foreach (var (amount, currency) in new[] { (10m, "usd"), (5m, "USD") })
        {
            var created = await admin.PostAsJsonAsync($"/api/clients/{clientId}/billable-items", new
            {
                description = "Charge",
                amount,
                currency,
                type = nameof(BillableItemType.OneTime),
            });
            created.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var response = await admin.GetAsync($"/api/clients/{clientId}/billable-items?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var totals = body.RootElement.GetProperty("uninvoicedTotal").EnumerateArray()
            .Select(t => (Currency: t.GetProperty("currency").GetString(), Amount: t.GetProperty("amount").GetDecimal()))
            .ToList();
        totals.Should().Equal((Currency: "USD", Amount: 15m));
    }

    /// <summary>The search and the type filter narrow both the uninvoiced and the invoiced items, and the uninvoiced total follows.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchAndTypeNarrowBothLists()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        var token = Guid.NewGuid().ToString("N")[..8];
        var ids = await BillableItemSeeds.SeedItemsAsync(
            factory,
            clientId,
            [
                ($"{token} hosting open", BillableItemType.OneTime, null),
                ($"{token} domain open", BillableItemType.Recurring, null),
                ($"{token} hosting billed", BillableItemType.OneTime, invoiceId),
                ($"{token} domain billed", BillableItemType.Recurring, invoiceId),
            ]);
        var scope = $"/api/clients/{clientId}/billable-items?page=1&pageSize=20";

        var everything = await ReadAsync(admin, $"{scope}&search={token}");
        var hosting = await ReadAsync(admin, $"{scope}&search={token.ToUpperInvariant()}%20HOSTING");
        var recurring = await ReadAsync(admin, $"{scope}&type={nameof(BillableItemType.Recurring)}");
        var both = await ReadAsync(admin, $"{scope}&search={token}&type=OneTime");

        everything.Uninvoiced.Should().Equal(ids[1], ids[0]);
        everything.Invoiced.Should().Equal(ids[3], ids[2]);
        hosting.Uninvoiced.Should().Equal(ids[0]);
        hosting.Invoiced.Should().Equal(ids[2]);
        hosting.TotalCount.Should().Be(1);
        hosting.TotalAmount.Should().Be(10m);
        recurring.Uninvoiced.Should().Equal(ids[1]);
        recurring.Invoiced.Should().Equal(ids[3]);
        both.Uninvoiced.Should().Equal(ids[0]);
        both.Invoiced.Should().Equal(ids[2]);
    }

    /// <summary>A whole number typed into the search finds the item with that id.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchFindsTheItemById()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var ids = await BillableItemSeeds.SeedItemsAsync(
            factory,
            clientId,
            [("Letters only one", BillableItemType.OneTime, null), ("Letters only two", BillableItemType.OneTime, null)]);

        var found = await ReadAsync(admin, $"/api/clients/{clientId}/billable-items?page=1&pageSize=20&search={ids[1]}");

        found.Uninvoiced.Should().Equal(ids[1]);
        found.Invoiced.Should().BeEmpty();
    }

    /// <summary>A percent sign and an underscore typed into the search match themselves, not any text.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchWildcardsAreLiteral()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var token = Guid.NewGuid().ToString("N")[..8];
        var ids = await BillableItemSeeds.SeedItemsAsync(
            factory,
            clientId,
            [
                ($"{token}-50%", BillableItemType.OneTime, null),
                ($"{token}-50X", BillableItemType.OneTime, null),
                ($"{token}-A_C", BillableItemType.OneTime, null),
                ($"{token}-ABC", BillableItemType.OneTime, null),
            ]);
        var scope = $"/api/clients/{clientId}/billable-items?page=1&pageSize=20";

        var percent = await ReadAsync(admin, $"{scope}&search={token}-50%25");
        var underscore = await ReadAsync(admin, $"{scope}&search={token}-A_C");
        var bareWildcard = await ReadAsync(admin, $"{scope}&search={token}%25%25");

        percent.Uninvoiced.Should().Equal(ids[0]);
        underscore.Uninvoiced.Should().Equal(ids[2]);
        bareWildcard.Uninvoiced.Should().BeEmpty();
    }

    /// <summary>The page and page size cut the invoiced list only, newest first, and its total counts every match.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task InvoicedItemsArePaged()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        var ids = await BillableItemSeeds.SeedItemsAsync(
            factory,
            clientId,
            [
                ("Billed a", BillableItemType.OneTime, invoiceId),
                ("Billed b", BillableItemType.OneTime, invoiceId),
                ("Billed c", BillableItemType.OneTime, invoiceId),
                ("Still open", BillableItemType.OneTime, null),
            ]);
        var scope = $"/api/clients/{clientId}/billable-items?pageSize=2";

        var first = await ReadAsync(admin, $"{scope}&page=1");
        var second = await ReadAsync(admin, $"{scope}&page=2");
        var beyond = await ReadAsync(admin, $"{scope}&page=3");

        first.Invoiced.Should().Equal(ids[2], ids[1]);
        first.TotalCount.Should().Be(3);
        first.Uninvoiced.Should().Equal(ids[3]);
        second.Invoiced.Should().Equal(ids[0]);
        second.Uninvoiced.Should().Equal(ids[3]);
        beyond.Invoiced.Should().BeEmpty();
    }

    /// <summary>A page below one or a page size outside the limit is refused with 400 naming the field, not corrected.</summary>
    /// <param name="queryString">The paging part of the query string.</param>
    /// <param name="field">The field the refusal must name.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("page=0&pageSize=20", "Page")]
    [InlineData("pageSize=20", "Page")]
    [InlineData("page=1&pageSize=0", "PageSize")]
    [InlineData("page=1&pageSize=101", "PageSize")]
    public async Task PagingOutsideTheLimitsIsRefused(string queryString, string field)
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients/{clientId}/billable-items?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.FieldMessagesAsync(response, field)).Should().NotBeEmpty();
    }

    /// <summary>A search longer than the limit is refused with 400 naming the search, not cut.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchOverTheLimitIsRefused()
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync(
            $"/api/clients/{clientId}/billable-items?page=1&pageSize=20&search={new string('a', PagingLimits.MaxSearchLength + 1)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.FieldMessagesAsync(response, nameof(ListClientBillableItemsQuery.Search))).Should().NotBeEmpty();
    }

    /// <summary>A client id in the route that is not a positive number is refused with 400 naming the client id.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ANonPositiveClientIdIsRefused()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/clients/0/billable-items?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.FieldMessagesAsync(response, nameof(ListClientBillableItemsQuery.ClientId))).Should().NotBeEmpty();
    }

    /// <summary>A type that names nothing, by name or by number, is refused with 400 naming the type, never read as no filter.</summary>
    /// <param name="filter">The filter's key and value.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("type=NotAType")]
    [InlineData("type=99")]
    public async Task AnUnknownTypeIsRefused(string filter)
    {
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/clients/{clientId}/billable-items?page=1&pageSize=20&{filter}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.FieldMessagesAsync(response, nameof(ListClientBillableItemsQuery.Type))).Should().NotBeEmpty();
    }

    /// <summary>Listing a client's billable items is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/clients/1/billable-items?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Reads one answer of the client's billable item list.</summary>
    /// <param name="admin">A client authenticated as Admin.</param>
    /// <param name="url">The list address with its query string.</param>
    /// <returns>The ids of both lists in the order given, the invoiced total count and the sum of the uninvoiced amounts.</returns>
    private static async Task<ClientItemsPage> ReadAsync(HttpClient admin, string url)
    {
        var root = await PagedListReads.PageAsync(admin, url);
        return new ClientItemsPage(
            root.GetProperty("uninvoicedItems").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).ToList(),
            PagedListReads.IdsOf(root.GetProperty("invoicedItems")),
            root.GetProperty("invoicedItems").GetProperty("totalCount").GetInt32(),
            root.GetProperty("uninvoicedTotal").EnumerateArray().Sum(t => t.GetProperty("amount").GetDecimal()));
    }

    /// <summary>What one answer of the client's billable item list held.</summary>
    /// <param name="Uninvoiced">The ids of the uninvoiced items.</param>
    /// <param name="Invoiced">The ids of the invoiced items of the page.</param>
    /// <param name="TotalCount">How many invoiced items match, across every page.</param>
    /// <param name="TotalAmount">The sum of the uninvoiced amounts.</param>
    private sealed record ClientItemsPage(List<int> Uninvoiced, List<int> Invoiced, int TotalCount, decimal TotalAmount);
}
