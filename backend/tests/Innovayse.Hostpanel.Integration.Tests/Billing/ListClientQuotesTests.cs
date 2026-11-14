using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/billing/quotes/client/{clientId}</c> against Postgres: the staff-facing, paginated
/// list of one client's own quotes.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListClientQuotesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An admin lists a client's quotes and finds the one just created for them.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListByClientIncludesTheClientsOwnQuote()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var quoteId = await BillingTestSeeds.CreateQuoteAsync(admin, clientId, 60m);

        var response = await admin.GetAsync($"/api/billing/quotes/client/{clientId}?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()).Should().Contain(quoteId);
    }

    /// <summary>A client with no quotes lists an empty page, not an error.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListByClientWithNoQuotesReturnsAnEmptyPage()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);

        var response = await admin.GetAsync($"/api/billing/quotes/client/{clientId}?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    /// <summary>Listing a client's quotes is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListByClientRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/billing/quotes/client/1?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>The list holds only the client of the route, and the route wins over a client id in the query string.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListHoldsOnlyTheClientOfTheRoute()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var other = await BillingTestSeeds.SeedClientAsync(factory);
        var own = await QuoteListTestSeeds.SeedAsync(factory, clientId, "Own", DateTimeOffset.UtcNow.AddDays(30));
        await QuoteListTestSeeds.SeedAsync(factory, other, "Other", DateTimeOffset.UtcNow.AddDays(30));

        var page = await InvoiceListTestSeeds.PageAsync(admin, $"/api/billing/quotes/client/{clientId}?page=1&pageSize=100&clientId={other}");

        InvoiceListTestSeeds.IdsOf(page).Should().Equal(own);
    }

    /// <summary>The search matches the subject case-insensitively, by id, and wildcards typed into it match themselves.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchFindsTheSubjectAndTheIdWithLiteralWildcards()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var expiry = DateTimeOffset.UtcNow.AddDays(30);
        var website = await QuoteListTestSeeds.SeedAsync(factory, clientId, "Website redesign", expiry);
        var percent = await QuoteListTestSeeds.SeedAsync(factory, clientId, "Launch 50% off", expiry);
        await QuoteListTestSeeds.SeedAsync(factory, clientId, "Launch 50x off", expiry);
        var underscore = await QuoteListTestSeeds.SeedAsync(factory, clientId, "Plan a_c", expiry);
        await QuoteListTestSeeds.SeedAsync(factory, clientId, "Plan abc", expiry);
        var route = $"/api/billing/quotes/client/{clientId}?page=1&pageSize=100";

        var bySubject = await InvoiceListTestSeeds.PageAsync(admin, $"{route}&search=WEBSITE");
        var byId = await InvoiceListTestSeeds.PageAsync(admin, $"{route}&search={website}");
        var byPercent = await InvoiceListTestSeeds.PageAsync(admin, $"{route}&search=50%25");
        var byUnderscore = await InvoiceListTestSeeds.PageAsync(admin, $"{route}&search=a_c");
        var bareWildcard = await InvoiceListTestSeeds.PageAsync(admin, $"{route}&search=%25%25");

        InvoiceListTestSeeds.IdsOf(bySubject).Should().Equal(website);
        InvoiceListTestSeeds.IdsOf(byId).Should().Contain(website);
        InvoiceListTestSeeds.IdsOf(byPercent).Should().Equal(percent);
        InvoiceListTestSeeds.IdsOf(byUnderscore).Should().Equal(underscore);
        InvoiceListTestSeeds.IdsOf(bareWildcard).Should().BeEmpty();
    }

    /// <summary>The stage and validity filters each narrow the client's quotes, and narrow together.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StageAndValidityFiltersNarrowTheClientsQuotes()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var ahead = DateTimeOffset.UtcNow.AddDays(30);
        var passed = DateTimeOffset.UtcNow.AddDays(-2);
        var draftAhead = await QuoteListTestSeeds.SeedAsync(factory, clientId, "Draft ahead", ahead);
        var deliveredAhead = await QuoteListTestSeeds.SeedAsync(factory, clientId, "Delivered ahead", ahead, QuoteStage.Delivered);
        var deliveredPassed = await QuoteListTestSeeds.SeedAsync(factory, clientId, "Delivered passed", passed, QuoteStage.Delivered);
        var route = $"/api/billing/quotes/client/{clientId}?page=1&pageSize=100";

        var delivered = await InvoiceListTestSeeds.PageAsync(admin, $"{route}&stage=Delivered");
        var valid = await InvoiceListTestSeeds.PageAsync(admin, $"{route}&validity=Valid");
        var expired = await InvoiceListTestSeeds.PageAsync(admin, $"{route}&validity=Expired");
        var deliveredValid = await InvoiceListTestSeeds.PageAsync(admin, $"{route}&stage=Delivered&validity=Valid");

        InvoiceListTestSeeds.IdsOf(delivered).Should().BeEquivalentTo([deliveredAhead, deliveredPassed]);
        InvoiceListTestSeeds.IdsOf(valid).Should().BeEquivalentTo([draftAhead, deliveredAhead]);
        InvoiceListTestSeeds.IdsOf(expired).Should().Equal(deliveredPassed);
        InvoiceListTestSeeds.IdsOf(deliveredValid).Should().Equal(deliveredAhead);
    }

    /// <summary>A filter is applied before paging: the total counts every match and each page holds its slice, newest first.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FilterIsAppliedBeforePaging()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var expiry = DateTimeOffset.UtcNow.AddDays(30);
        var oldest = await QuoteListTestSeeds.SeedAsync(factory, clientId, "Oldest", expiry, QuoteStage.Delivered, TestMoments.Seeded);
        await QuoteListTestSeeds.SeedAsync(factory, clientId, "Draft", expiry, createdAt: TestMoments.Seeded.AddDays(1));
        var newest = await QuoteListTestSeeds.SeedAsync(factory, clientId, "Newest", expiry, QuoteStage.Delivered, TestMoments.Seeded.AddDays(2));
        var route = $"/api/billing/quotes/client/{clientId}";

        var first = await InvoiceListTestSeeds.PageAsync(admin, $"{route}?page=1&pageSize=1&stage=Delivered");
        var second = await InvoiceListTestSeeds.PageAsync(admin, $"{route}?page=2&pageSize=1&stage=Delivered");
        var beyond = await InvoiceListTestSeeds.PageAsync(admin, $"{route}?page=3&pageSize=1&stage=Delivered");

        InvoiceListTestSeeds.IdsOf(first).Should().Equal(newest);
        first.GetProperty("totalCount").GetInt32().Should().Be(2);
        InvoiceListTestSeeds.IdsOf(second).Should().Equal(oldest);
        InvoiceListTestSeeds.IdsOf(beyond).Should().BeEmpty();
    }

    /// <summary>A page below one, no page, or a page size outside the limit is refused with 400 on the page and page size, not corrected.</summary>
    /// <param name="queryString">The paging part of the query string.</param>
    /// <param name="field">The field the refusal must name.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("page=0&pageSize=20", "page")]
    [InlineData("pageSize=20", "page")]
    [InlineData("page=1&pageSize=0", "pagesize")]
    [InlineData("page=1&pageSize=101", "pagesize")]
    public async Task PagingOutsideTheLimitsIsRefused(string queryString, string field)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/billing/quotes/client/1?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await InvoiceListTestSeeds.RefusedFieldsAsync(response)).Should().Contain(field);
    }

    /// <summary>A search longer than the limit is refused with 400 on the search, not cut.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchOverTheLimitIsRefused()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync(
            $"/api/billing/quotes/client/1?page=1&pageSize=20&search={new string('a', PagingLimits.MaxSearchLength + 1)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await InvoiceListTestSeeds.RefusedFieldsAsync(response)).Should().Contain("search");
    }

    /// <summary>A route client id that names nothing is refused with 400 on the client id.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task NonPositiveClientIsRefused()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync("/api/billing/quotes/client/0?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await InvoiceListTestSeeds.RefusedFieldsAsync(response)).Should().Contain("clientid");
    }

    /// <summary>
    /// A stage or validity that names nothing, by name or by number, is refused with 400: the model binder turns it back
    /// before the validator runs, so the refusal is the validation code without a field of the validator's own.
    /// </summary>
    /// <param name="filter">The filter's key and value.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("stage=Archived")]
    [InlineData("stage=99")]
    [InlineData("validity=Maybe")]
    [InlineData("validity=99")]
    public async Task UnknownEnumValueIsRefused(string filter)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/billing/quotes/client/1?page=1&pageSize=20&{filter}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
