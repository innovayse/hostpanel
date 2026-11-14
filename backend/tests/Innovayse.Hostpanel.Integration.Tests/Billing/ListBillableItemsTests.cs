using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Billing.Queries.ListBillableItems;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/billing/billable-items</c> against Postgres: the admin-wide, paginated billable item list, narrowed by
/// the search on the description and id and by the client, type and invoiced filters in SQL ahead of paging, so every
/// row is reachable however many there are.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListBillableItemsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A billable item just created for a client appears in the admin-wide list, named after its client.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListIncludesAFreshlyCreatedBillableItem()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var created = await admin.PostAsJsonAsync($"/api/clients/{clientId}/billable-items", new
        {
            description = "Admin-wide list check",
            amount = 30m,
            currency = "USD",
            type = nameof(BillableItemType.OneTime),
        });
        var itemId = await created.Content.ReadFromJsonAsync<int>();

        var response = await admin.GetAsync($"/api/billing/billable-items?page=1&pageSize=100&clientId={clientId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var item = body.RootElement.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == itemId);
        item.GetProperty("clientName").GetString().Should().Be("Ada Lovelace");
    }

    /// <summary>
    /// More rows than a page holds are all reachable: the first page holds the newest hundred, the second the rest, and the
    /// total counts every match, not only the first hundred.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task RowsPastTheFirstHundredAreReachable()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var ids = await BillableItemSeeds.SeedManyItemsAsync(factory, clientId, "Past the first hundred", 105);
        var newestFirst = ids.OrderByDescending(id => id).ToList();
        var scope = $"/api/billing/billable-items?pageSize=100&clientId={clientId}";

        var (first, firstCount) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&page=1");
        var (second, secondCount) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&page=2");

        first.Should().Equal(newestFirst.Take(100));
        second.Should().Equal(newestFirst.Skip(100));
        firstCount.Should().Be(105);
        secondCount.Should().Be(105);
    }

    /// <summary>The search matches the description case-insensitively as a substring and leaves other items out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchFindsTheDescriptionInAnyLetterCase()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var token = Guid.NewGuid().ToString("N")[..8];
        var ids = await BillableItemSeeds.SeedItemsAsync(
            factory,
            clientId,
            [
                ($"{token} hosting Alpha", BillableItemType.OneTime, null),
                ($"{token} domain Beta", BillableItemType.OneTime, null),
                ($"{token} hosting Gamma", BillableItemType.OneTime, null),
            ]);
        var scope = $"/api/billing/billable-items?page=1&pageSize=100&clientId={clientId}";

        var (hosting, hostingCount) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&search={token.ToUpperInvariant()}%20HOSTING");
        var (everything, everythingCount) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&search={token}");

        hosting.Should().Equal(ids[2], ids[0]);
        hostingCount.Should().Be(2);
        everything.Should().Equal(ids[2], ids[1], ids[0]);
        everythingCount.Should().Be(3);
    }

    /// <summary>A whole number typed into the search finds the item with that id.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchFindsTheItemById()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var ids = await BillableItemSeeds.SeedItemsAsync(
            factory,
            clientId,
            [("Letters only one", BillableItemType.OneTime, null), ("Letters only two", BillableItemType.OneTime, null)]);

        var (found, count) = await PagedListReads.ReadIdsAsync(
            admin, $"/api/billing/billable-items?page=1&pageSize=100&clientId={clientId}&search={ids[0]}");

        found.Should().Equal(ids[0]);
        count.Should().Be(1);
    }

    /// <summary>A percent sign and an underscore typed into the search match themselves, not any text.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchWildcardsAreLiteral()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
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
        var scope = $"/api/billing/billable-items?page=1&pageSize=100&clientId={clientId}";

        var (percent, _) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&search={token}-50%25");
        var (underscore, _) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&search={token}-A_C");
        var (bareWildcard, bareCount) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&search={token}%25%25");

        percent.Should().Equal(ids[0]);
        underscore.Should().Equal(ids[2]);
        bareWildcard.Should().BeEmpty();
        bareCount.Should().Be(0);
    }

    /// <summary>The client filter keeps only the items of that client.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ClientFilterKeepsOnlyThatClient()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var first = await BillingTestSeeds.SeedClientAsync(factory);
        var second = await BillingTestSeeds.SeedClientAsync(factory);
        var firstIds = await BillableItemSeeds.SeedItemsAsync(factory, first, [("One", BillableItemType.OneTime, null)]);
        await BillableItemSeeds.SeedItemsAsync(factory, second, [("Two", BillableItemType.OneTime, null)]);

        var (ids, count) = await PagedListReads.ReadIdsAsync(admin, $"/api/billing/billable-items?page=1&pageSize=100&clientId={first}");

        ids.Should().Equal(firstIds);
        count.Should().Be(1);
    }

    /// <summary>
    /// The type and the invoiced filters, alone and together, cover the six statuses of the screen: uninvoiced, invoiced,
    /// one time, recurring, active recurring and completed recurring.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TypeAndInvoicedFiltersNarrowTogether()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 10m);
        var ids = await BillableItemSeeds.SeedItemsAsync(
            factory,
            clientId,
            [
                ("One time open", BillableItemType.OneTime, null),
                ("One time billed", BillableItemType.OneTime, invoiceId),
                ("Recurring open", BillableItemType.Recurring, null),
                ("Recurring billed", BillableItemType.Recurring, invoiceId),
            ]);
        var scope = $"/api/billing/billable-items?page=1&pageSize=100&clientId={clientId}";

        var (uninvoiced, _) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&invoiced=false");
        var (invoiced, _) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&invoiced=true");
        var (recurring, _) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&type={nameof(BillableItemType.Recurring)}");
        var (oneTime, _) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&type={nameof(BillableItemType.OneTime)}");
        var (activeRecurring, _) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&type=Recurring&invoiced=false");
        var (completedRecurring, completedCount) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&type=Recurring&invoiced=true");

        uninvoiced.Should().Equal(ids[2], ids[0]);
        invoiced.Should().Equal(ids[3], ids[1]);
        recurring.Should().Equal(ids[3], ids[2]);
        oneTime.Should().Equal(ids[1], ids[0]);
        activeRecurring.Should().Equal(ids[2]);
        completedRecurring.Should().Equal(ids[3]);
        completedCount.Should().Be(1);
    }

    /// <summary>The search and the filters narrow together, and they are applied before paging: the total counts every match.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FiltersAreAppliedBeforePaging()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var token = Guid.NewGuid().ToString("N")[..8];
        var ids = await BillableItemSeeds.SeedItemsAsync(
            factory,
            clientId,
            [
                ($"{token} a", BillableItemType.Recurring, null),
                ($"{token} b", BillableItemType.OneTime, null),
                ($"{token} c", BillableItemType.Recurring, null),
                ($"{token} d", BillableItemType.Recurring, null),
            ]);
        var scope = $"/api/billing/billable-items?pageSize=2&clientId={clientId}&search={token}&type=Recurring";

        var (first, firstCount) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&page=1");
        var (second, secondCount) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&page=2");
        var (beyond, _) = await PagedListReads.ReadIdsAsync(admin, $"{scope}&page=3");

        first.Should().Equal(ids[3], ids[2]);
        firstCount.Should().Be(3);
        second.Should().Equal(ids[0]);
        secondCount.Should().Be(3);
        beyond.Should().BeEmpty();
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
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/billing/billable-items?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.FieldMessagesAsync(response, field)).Should().NotBeEmpty();
    }

    /// <summary>A search longer than the limit is refused with 400 naming the search, not cut.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchOverTheLimitIsRefused()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync(
            $"/api/billing/billable-items?page=1&pageSize=20&search={new string('a', PagingLimits.MaxSearchLength + 1)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
        (await ServicesTestSeeds.FieldMessagesAsync(response, nameof(ListBillableItemsQuery.Search))).Should().NotBeEmpty();
    }

    /// <summary>A client id that is not a positive number is refused with 400 naming the client id.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ANonPositiveClientIdIsRefused()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/billing/billable-items?page=1&pageSize=20&clientId=0");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.FieldMessagesAsync(response, nameof(ListBillableItemsQuery.ClientId))).Should().NotBeEmpty();
    }

    /// <summary>
    /// A type that names nothing, by name or by number, and an invoiced flag that is no boolean are refused with 400 naming the
    /// field, never read as no filter. The model binder refuses them before the validator runs.
    /// </summary>
    /// <param name="filter">The filter's key and value.</param>
    /// <param name="field">The field the refusal must name.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("type=NotAType", "Type")]
    [InlineData("type=99", "Type")]
    [InlineData("invoiced=maybe", "Invoiced")]
    public async Task AnUnknownFilterValueIsRefused(string filter, string field)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/billing/billable-items?page=1&pageSize=100&{filter}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ServicesTestSeeds.FieldMessagesAsync(response, field)).Should().NotBeEmpty();
    }

    /// <summary>Listing billable items is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/billing/billable-items?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
