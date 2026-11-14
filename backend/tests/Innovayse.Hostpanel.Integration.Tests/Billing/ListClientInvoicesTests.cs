using System.Net;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/billing/client/{clientId}</c> against Postgres: the staff-facing, paginated list
/// of one client's own invoices.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListClientInvoicesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin lists a client's invoices and finds the one just created for them.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListByClientIncludesTheClientsOwnInvoice()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 7m);

        var response = await admin.GetAsync($"/api/billing/client/{clientId}?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()).Should().Contain(invoiceId);
    }

    /// <summary>A client with no invoices lists an empty page, not an error.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListByClientWithNoInvoicesReturnsAnEmptyPage()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);

        var response = await admin.GetAsync($"/api/billing/client/{clientId}?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    /// <summary>Listing a client's invoices is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListByClientRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/billing/client/1");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>The list holds only the client's own invoices, whatever else exists.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListByClientLeavesOutOtherClientsInvoicesAsync()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var otherClientId = await BillingTestSeeds.SeedClientAsync(factory);
        var mine = await InvoiceListTestSeeds.SeedAsync(factory, clientId);
        await InvoiceListTestSeeds.SeedAsync(factory, otherClientId);

        var page = await InvoiceListTestSeeds.PageAsync(admin, $"/api/billing/client/{clientId}?page=1&pageSize=100");

        InvoiceListTestSeeds.IdsOf(page).Should().Equal(mine);
    }

    /// <summary>The search matches the payment method and the id, and a literal percent sign or underscore matches itself.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchMatchesThePaymentMethodAndTheIdLiterallyAsync()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var token = InvoiceListTestSeeds.UniqueToken();
        var literal = await InvoiceListTestSeeds.SeedAsync(factory, clientId, paymentMethod: $"a%b_{token}");
        var lookalike = await InvoiceListTestSeeds.SeedAsync(factory, clientId, paymentMethod: $"aXbY{token}");

        var byMethod = await InvoiceListTestSeeds.PageAsync(
            admin, $"/api/billing/client/{clientId}?page=1&pageSize=100&search={Uri.EscapeDataString($"A%B_{token}")}");
        var byId = await InvoiceListTestSeeds.PageAsync(admin, $"/api/billing/client/{clientId}?page=1&pageSize=100&search={lookalike}");

        InvoiceListTestSeeds.IdsOf(byMethod).Should().Equal(literal);
        InvoiceListTestSeeds.IdsOf(byId).Should().Contain(lookalike).And.NotContain(literal);
    }

    /// <summary>The status filter keeps only that status.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StatusFilterKeepsOnlyThatStatusAsync()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await InvoiceListTestSeeds.SeedAsync(factory, clientId);
        var draft = await InvoiceListTestSeeds.SeedAsync(factory, clientId, isDraft: true);

        var page = await InvoiceListTestSeeds.PageAsync(admin, $"/api/billing/client/{clientId}?page=1&pageSize=100&status=Draft");

        InvoiceListTestSeeds.IdsOf(page).Should().Equal(draft);
    }

    /// <summary>The period keeps the whole last day, up to its last second, and leaves the neighbouring days out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PeriodIncludesTheWholeLastDayAsync()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await InvoiceListTestSeeds.SeedAsync(factory, clientId, new DateTimeOffset(2020, 3, 9, 23, 59, 59, TimeSpan.Zero));
        var firstInstant = await InvoiceListTestSeeds.SeedAsync(factory, clientId, new DateTimeOffset(2020, 3, 10, 0, 0, 0, TimeSpan.Zero));
        var lastInstant = await InvoiceListTestSeeds.SeedAsync(factory, clientId, new DateTimeOffset(2020, 3, 10, 23, 59, 59, TimeSpan.Zero));
        await InvoiceListTestSeeds.SeedAsync(factory, clientId, new DateTimeOffset(2020, 3, 11, 0, 0, 0, TimeSpan.Zero));

        var page = await InvoiceListTestSeeds.PageAsync(
            admin, $"/api/billing/client/{clientId}?page=1&pageSize=100&from=2020-03-10T00:00:00Z&to=2020-03-10T00:00:00Z");

        InvoiceListTestSeeds.IdsOf(page).Should().BeEquivalentTo([firstInstant, lastInstant]);
    }

    /// <summary>A page holds the newest invoices first and the total counts every match.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PagingSlicesTheNewestFirstOrderAsync()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var oldest = await InvoiceListTestSeeds.SeedAsync(factory, clientId, new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var middle = await InvoiceListTestSeeds.SeedAsync(factory, clientId, new DateTimeOffset(2021, 1, 2, 0, 0, 0, TimeSpan.Zero));
        var newest = await InvoiceListTestSeeds.SeedAsync(factory, clientId, new DateTimeOffset(2021, 1, 3, 0, 0, 0, TimeSpan.Zero));

        var first = await InvoiceListTestSeeds.PageAsync(admin, $"/api/billing/client/{clientId}?page=1&pageSize=2");
        var second = await InvoiceListTestSeeds.PageAsync(admin, $"/api/billing/client/{clientId}?page=2&pageSize=2");

        InvoiceListTestSeeds.IdsOf(first).Should().Equal(newest, middle);
        InvoiceListTestSeeds.IdsOf(second).Should().Equal(oldest);
        first.GetProperty("totalCount").GetInt32().Should().Be(3);
    }

    /// <summary>A value out of range is refused with 400 and names the field, never clamped.</summary>
    /// <param name="queryString">The query string sent.</param>
    /// <param name="field">The field the refusal must name, in lower case.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("page=0&pageSize=20", "page")]
    [InlineData("page=1&pageSize=101", "pagesize")]
    [InlineData("page=1&pageSize=20&status=99", "status")]
    public async Task OutOfRangeValueIsRefusedNamingTheFieldAsync(string queryString, string field)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/billing/client/1?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await InvoiceListTestSeeds.RefusedFieldsAsync(response)).Should().Contain(field);
    }
}
