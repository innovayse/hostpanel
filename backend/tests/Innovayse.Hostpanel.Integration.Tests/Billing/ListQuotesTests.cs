using System.Net;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Constants;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/billing/quotes</c> against Postgres: the admin-wide, paginated quote list.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListQuotesTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A quote just created appears in the admin's list, named after its client.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListIncludesAFreshlyCreatedQuote()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var quoteId = await BillingTestSeeds.CreateQuoteAsync(admin, clientId, 90m);

        var response = await admin.GetAsync("/api/billing/quotes?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()).Should().Contain(quoteId);
        body.RootElement.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("id").GetInt32() == quoteId)
            .GetProperty("clientName").GetString().Should().Be("Ada Lovelace");
    }

    /// <summary>A quote is priced in its client's currency, and the list and the detail both say so.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task QuoteCarriesTheClientsCurrency()
    {
        await BillingTestSeeds.EnsureCurrencyAsync(factory, "ZQM", "997", 0.5m);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory, "ZQM");
        var quoteId = await BillingTestSeeds.CreateQuoteAsync(admin, clientId, 90m);

        var list = await admin.GetAsync("/api/billing/quotes?page=1&pageSize=100");
        var detail = await admin.GetAsync($"/api/billing/quotes/{quoteId}");

        using var listBody = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        listBody.RootElement.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("id").GetInt32() == quoteId)
            .GetProperty("currency").GetString().Should().Be("ZQM");
        using var detailBody = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        detailBody.RootElement.GetProperty("currency").GetString().Should().Be("ZQM");
    }

    /// <summary>Listing quotes is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/billing/quotes?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>The search matches the subject case-insensitively as a substring and leaves other quotes out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchFindsTheSubjectInAnyLetterCase()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var token = Guid.NewGuid().ToString("N")[..10];
        var alpha = await QuoteListTestSeeds.SeedAsync(factory, clientId, $"Alpha-{token}", QuoteListTestSeeds.ExpiresAhead);
        await QuoteListTestSeeds.SeedAsync(factory, clientId, $"Beta-{token}", QuoteListTestSeeds.ExpiresAhead);

        var page = await PagedListReads.PageAsync(admin, $"/api/billing/quotes?page=1&pageSize=100&search=ALPHA-{token.ToUpperInvariant()}");

        PagedListReads.IdsOf(page).Should().Equal(alpha);
        page.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    /// <summary>A whole number typed into the search finds the quote with that id, so staff can type an id straight in.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchFindsTheQuoteById()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var hosting = await QuoteListTestSeeds.SeedAsync(factory, clientId, "Hosting upgrade", QuoteListTestSeeds.ExpiresAhead);
        await QuoteListTestSeeds.SeedAsync(factory, clientId, "Domain transfer", QuoteListTestSeeds.ExpiresAhead);

        var page = await PagedListReads.PageAsync(admin, $"/api/billing/quotes?page=1&pageSize=100&clientId={clientId}&search={hosting}");

        PagedListReads.IdsOf(page).Should().Equal(hosting);
    }

    /// <summary>A percent sign and an underscore typed into the search match themselves, not any text.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchWildcardsAreLiteral()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var token = Guid.NewGuid().ToString("N")[..10];
        var expiry = QuoteListTestSeeds.ExpiresAhead;
        var percent = await QuoteListTestSeeds.SeedAsync(factory, clientId, $"{token} 50% off", expiry);
        await QuoteListTestSeeds.SeedAsync(factory, clientId, $"{token} 50x off", expiry);
        var underscore = await QuoteListTestSeeds.SeedAsync(factory, clientId, $"{token} a_c", expiry);
        await QuoteListTestSeeds.SeedAsync(factory, clientId, $"{token} abc", expiry);

        var percentPage = await PagedListReads.PageAsync(admin, $"/api/billing/quotes?page=1&pageSize=100&search={token}%2050%25");
        var underscorePage = await PagedListReads.PageAsync(admin, $"/api/billing/quotes?page=1&pageSize=100&search={token}%20a_c");
        var bareWildcard = await PagedListReads.PageAsync(admin, $"/api/billing/quotes?page=1&pageSize=100&search={token}%25%25");

        PagedListReads.IdsOf(percentPage).Should().Equal(percent);
        PagedListReads.IdsOf(underscorePage).Should().Equal(underscore);
        PagedListReads.IdsOf(bareWildcard).Should().BeEmpty();
    }

    /// <summary>The client filter keeps only that client's quotes.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ClientFilterKeepsOnlyThatClientsQuotes()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var first = await BillingTestSeeds.SeedClientAsync(factory);
        var second = await BillingTestSeeds.SeedClientAsync(factory);
        var firstQuote = await QuoteListTestSeeds.SeedAsync(factory, first, "First", QuoteListTestSeeds.ExpiresAhead);
        var secondQuote = await QuoteListTestSeeds.SeedAsync(factory, second, "Second", QuoteListTestSeeds.ExpiresAhead);

        var firstPage = await PagedListReads.PageAsync(admin, $"/api/billing/quotes?page=1&pageSize=100&clientId={first}");
        var secondPage = await PagedListReads.PageAsync(admin, $"/api/billing/quotes?page=1&pageSize=100&clientId={second}");

        PagedListReads.IdsOf(firstPage).Should().Equal(firstQuote);
        PagedListReads.IdsOf(secondPage).Should().Equal(secondQuote);
    }

    /// <summary>The stage filter keeps only the quotes in that stage.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task StageFilterKeepsOnlyThatStage()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var expiry = QuoteListTestSeeds.ExpiresAhead;
        await QuoteListTestSeeds.SeedAsync(factory, clientId, "Draft", expiry);
        var delivered = await QuoteListTestSeeds.SeedAsync(factory, clientId, "Delivered", expiry, QuoteStage.Delivered);
        var accepted = await QuoteListTestSeeds.SeedAsync(factory, clientId, "Accepted", expiry, QuoteStage.Accepted);

        var deliveredPage = await PagedListReads.PageAsync(admin, $"/api/billing/quotes?page=1&pageSize=100&clientId={clientId}&stage=Delivered");
        var acceptedPage = await PagedListReads.PageAsync(admin, $"/api/billing/quotes?page=1&pageSize=100&clientId={clientId}&stage=Accepted");

        PagedListReads.IdsOf(deliveredPage).Should().Equal(delivered);
        PagedListReads.IdsOf(acceptedPage).Should().Equal(accepted);
    }

    /// <summary>
    /// The validity filter tells the quotes whose expiry date is ahead from the ones whose date has passed, by the date
    /// alone: a quote in the Expired stage with a date still ahead is valid here.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ValidityFilterJudgesTheExpiryDate()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var ahead = await QuoteListTestSeeds.SeedAsync(factory, clientId, "Ahead", QuoteListTestSeeds.ExpiresAhead);
        var expiredStageAhead = await QuoteListTestSeeds.SeedAsync(
            factory, clientId, "Expired stage", QuoteListTestSeeds.ExpiresAhead, QuoteStage.Expired);
        var passed = await QuoteListTestSeeds.SeedAsync(factory, clientId, "Passed", QuoteListTestSeeds.ExpiredBefore, QuoteStage.Delivered);

        var valid = await PagedListReads.PageAsync(admin, $"/api/billing/quotes?page=1&pageSize=100&clientId={clientId}&validity=Valid");
        var expired = await PagedListReads.PageAsync(admin, $"/api/billing/quotes?page=1&pageSize=100&clientId={clientId}&validity=Expired");

        PagedListReads.IdsOf(valid).Should().BeEquivalentTo([ahead, expiredStageAhead]);
        PagedListReads.IdsOf(expired).Should().Equal(passed);
    }

    /// <summary>The search and every filter narrow together: a quote must pass each one.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CombinedFiltersAllApply()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var other = await BillingTestSeeds.SeedClientAsync(factory);
        var token = Guid.NewGuid().ToString("N")[..10];
        var ahead = QuoteListTestSeeds.ExpiresAhead;
        var match = await QuoteListTestSeeds.SeedAsync(factory, clientId, $"Alpha {token}", ahead, QuoteStage.Delivered);
        await QuoteListTestSeeds.SeedAsync(factory, clientId, $"Alpha {token}", ahead, QuoteStage.Accepted);
        await QuoteListTestSeeds.SeedAsync(factory, clientId, $"Alpha {token}", QuoteListTestSeeds.ExpiredBefore, QuoteStage.Delivered);
        await QuoteListTestSeeds.SeedAsync(factory, clientId, $"Beta {token}", ahead, QuoteStage.Delivered);
        await QuoteListTestSeeds.SeedAsync(factory, other, $"Alpha {token}", ahead, QuoteStage.Delivered);

        var page = await PagedListReads.PageAsync(
            admin, $"/api/billing/quotes?page=1&pageSize=100&search=alpha%20{token}&clientId={clientId}&stage=Delivered&validity=Valid");

        PagedListReads.IdsOf(page).Should().Equal(match);
    }

    /// <summary>A filter is applied before paging: the total counts every match, each page holds its slice, newest first and the id breaks a tie.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FilterIsAppliedBeforePaging()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var expiry = QuoteListTestSeeds.ExpiresAhead;
        var oldest = await QuoteListTestSeeds.SeedAsync(factory, clientId, "Oldest", expiry, createdAt: TestMoments.Seeded);
        var sameMoment = TestMoments.Seeded.AddDays(1);
        var tiedFirst = await QuoteListTestSeeds.SeedAsync(factory, clientId, "Tied first", expiry, createdAt: sameMoment);
        var tiedSecond = await QuoteListTestSeeds.SeedAsync(factory, clientId, "Tied second", expiry, createdAt: sameMoment);
        var newest = await QuoteListTestSeeds.SeedAsync(factory, clientId, "Newest", expiry, createdAt: TestMoments.Seeded.AddDays(2));

        var first = await PagedListReads.PageAsync(admin, $"/api/billing/quotes?page=1&pageSize=3&clientId={clientId}");
        var second = await PagedListReads.PageAsync(admin, $"/api/billing/quotes?page=2&pageSize=3&clientId={clientId}");
        var beyond = await PagedListReads.PageAsync(admin, $"/api/billing/quotes?page=3&pageSize=3&clientId={clientId}");

        PagedListReads.IdsOf(first).Should().Equal(newest, tiedSecond, tiedFirst);
        first.GetProperty("totalCount").GetInt32().Should().Be(4);
        PagedListReads.IdsOf(second).Should().Equal(oldest);
        second.GetProperty("totalCount").GetInt32().Should().Be(4);
        PagedListReads.IdsOf(beyond).Should().BeEmpty();
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

        var response = await admin.GetAsync($"/api/billing/quotes?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PagedListReads.RefusedFieldsAsync(response)).Should().Contain(field);
    }

    /// <summary>A search longer than the limit is refused with 400 on the search, not cut.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchOverTheLimitIsRefused()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/billing/quotes?page=1&pageSize=20&search={new string('a', PagingLimits.MaxSearchLength + 1)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PagedListReads.RefusedFieldsAsync(response)).Should().Contain("search");
    }

    /// <summary>A client id that names nothing is refused with 400 on the client id.</summary>
    /// <param name="clientId">The client id sent.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("0")]
    [InlineData("-4")]
    public async Task NonPositiveClientIsRefused(string clientId)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/billing/quotes?page=1&pageSize=20&clientId={clientId}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PagedListReads.RefusedFieldsAsync(response)).Should().Contain("clientid");
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

        var response = await admin.GetAsync($"/api/billing/quotes?page=1&pageSize=20&{filter}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }
}
