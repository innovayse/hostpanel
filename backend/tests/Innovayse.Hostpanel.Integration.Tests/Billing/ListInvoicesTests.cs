using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/billing</c> against Postgres: the admin-wide, paginated invoice list.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListInvoicesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>Listing invoices is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetInvoicesAsync_Returns401_WithoutAuthAsync()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/billing");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>An admin lists invoices and sees the page.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetInvoicesAsync_Returns200_AsAdminAsync()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/billing?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>An invoice just created for a client appears in the admin's list, named after its client.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListIncludesAFreshlyCreatedInvoice()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await BillingTestSeeds.CreateInvoiceAsync(admin, clientId, 5m);

        var response = await admin.GetAsync("/api/billing?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()).Should().Contain(invoiceId);
        body.RootElement.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("id").GetInt32() == invoiceId)
            .GetProperty("clientName").GetString().Should().Be("Ada Lovelace");
    }

    /// <summary>Every row of the list names the currency its amounts are in, so a list can print a figure with the right unit.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRowsCarryTheInvoiceCurrencyAsync()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        int invoiceId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
            var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(14), "EUR", DateTimeOffset.UtcNow);
            invoice.AddItem("Hosting", 12m, 1, null);
            context.Invoices.Add(invoice);
            await context.SaveChangesAsync(CancellationToken.None);
            invoiceId = invoice.Id;
        }

        var response = await admin.GetAsync("/api/billing?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var row = body.RootElement.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == invoiceId);
        row.GetProperty("currency").GetString().Should().Be("EUR");
    }

    /// <summary>A status filter that names no invoice status is refused instead of silently ignored.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task UnknownStatusFilterIsRefused()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/billing?page=1&pageSize=20&status=NotAStatus");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>The search matches the payment method in any letter case and the invoice id, and nothing else.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchMatchesThePaymentMethodAndTheIdAsync()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var token = InvoiceListTestSeeds.UniqueToken();
        var byMethod = await InvoiceListTestSeeds.SeedAsync(factory, clientId, paymentMethod: $"Wire-{token}");
        var other = await InvoiceListTestSeeds.SeedAsync(factory, clientId, paymentMethod: "card");

        var methodPage = await InvoiceListTestSeeds.PageAsync(admin, $"/api/billing?page=1&pageSize=100&clientId={clientId}&search=wire-{token}");
        var idPage = await InvoiceListTestSeeds.PageAsync(admin, $"/api/billing?page=1&pageSize=100&clientId={clientId}&search={other}");

        InvoiceListTestSeeds.IdsOf(methodPage).Should().Equal(byMethod);
        InvoiceListTestSeeds.IdsOf(idPage).Should().Contain(other).And.NotContain(byMethod);
    }

    /// <summary>A literal percent sign or underscore in the search matches itself, not any text.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchTreatsPercentAndUnderscoreAsLiteralTextAsync()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var token = InvoiceListTestSeeds.UniqueToken();
        var literal = await InvoiceListTestSeeds.SeedAsync(factory, clientId, paymentMethod: $"a%b_{token}");
        await InvoiceListTestSeeds.SeedAsync(factory, clientId, paymentMethod: $"aXbY{token}");

        var page = await InvoiceListTestSeeds.PageAsync(
            admin, $"/api/billing?page=1&pageSize=100&clientId={clientId}&search={Uri.EscapeDataString($"a%b_{token}")}");

        InvoiceListTestSeeds.IdsOf(page).Should().Equal(literal);
    }

    /// <summary>The client filter keeps only that client's invoices.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ClientFilterKeepsOnlyThatClientsInvoicesAsync()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var otherClientId = await BillingTestSeeds.SeedClientAsync(factory);
        var mine = await InvoiceListTestSeeds.SeedAsync(factory, clientId);
        await InvoiceListTestSeeds.SeedAsync(factory, otherClientId);

        var page = await InvoiceListTestSeeds.PageAsync(admin, $"/api/billing?page=1&pageSize=100&clientId={clientId}");

        InvoiceListTestSeeds.IdsOf(page).Should().Equal(mine);
    }

    /// <summary>The status filter keeps only the invoices in that state.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StatusFilterKeepsOnlyThatStatusAsync()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        await InvoiceListTestSeeds.SeedAsync(factory, clientId);
        var draft = await InvoiceListTestSeeds.SeedAsync(factory, clientId, isDraft: true);

        var page = await InvoiceListTestSeeds.PageAsync(admin, $"/api/billing?page=1&pageSize=100&clientId={clientId}&status=Draft");

        InvoiceListTestSeeds.IdsOf(page).Should().Equal(draft);
    }

    /// <summary>The period keeps the first day from its first instant and the last day to its last instant, whatever time the bound carries.</summary>
    /// <param name="to">The upper bound sent, a moment of the last day.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("2020-03-10T00:00:00Z")]
    [InlineData("2020-03-10T08:30:00Z")]
    [InlineData("2020-03-10")]
    public async Task PeriodIncludesTheWholeFirstAndLastDayAsync(string to)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var before = await InvoiceListTestSeeds.SeedAsync(factory, clientId, new DateTimeOffset(2020, 3, 9, 23, 59, 59, TimeSpan.Zero));
        var firstInstant = await InvoiceListTestSeeds.SeedAsync(factory, clientId, new DateTimeOffset(2020, 3, 10, 0, 0, 0, TimeSpan.Zero));
        var lastInstant = await InvoiceListTestSeeds.SeedAsync(factory, clientId, new DateTimeOffset(2020, 3, 10, 23, 59, 59, TimeSpan.Zero));
        var after = await InvoiceListTestSeeds.SeedAsync(factory, clientId, new DateTimeOffset(2020, 3, 11, 0, 0, 0, TimeSpan.Zero));

        var page = await InvoiceListTestSeeds.PageAsync(
            admin, $"/api/billing?page=1&pageSize=100&clientId={clientId}&from=2020-03-10T00:00:00Z&to={Uri.EscapeDataString(to)}");

        var ids = InvoiceListTestSeeds.IdsOf(page);
        ids.Should().BeEquivalentTo([firstInstant, lastInstant]);
        ids.Should().NotContain([before, after]);
    }

    /// <summary>A page holds the newest invoices first, the total counts every match and the next page continues where it stopped.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PagingSlicesTheNewestFirstOrderAsync()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var oldest = await InvoiceListTestSeeds.SeedAsync(factory, clientId, new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var middle = await InvoiceListTestSeeds.SeedAsync(factory, clientId, new DateTimeOffset(2021, 1, 2, 0, 0, 0, TimeSpan.Zero));
        var newest = await InvoiceListTestSeeds.SeedAsync(factory, clientId, new DateTimeOffset(2021, 1, 3, 0, 0, 0, TimeSpan.Zero));

        var first = await InvoiceListTestSeeds.PageAsync(admin, $"/api/billing?page=1&pageSize=2&clientId={clientId}");
        var second = await InvoiceListTestSeeds.PageAsync(admin, $"/api/billing?page=2&pageSize=2&clientId={clientId}");

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
    [InlineData("page=1&pageSize=0", "pagesize")]
    [InlineData("page=1&pageSize=101", "pagesize")]
    [InlineData("page=1&pageSize=20&clientId=0", "clientid")]
    [InlineData("page=1&pageSize=20&status=99", "status")]
    public async Task OutOfRangeValueIsRefusedNamingTheFieldAsync(string queryString, string field)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/billing?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await InvoiceListTestSeeds.RefusedFieldsAsync(response)).Should().Contain(field);
    }
}
