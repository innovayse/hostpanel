using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Constants;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing;

/// <summary>
/// <c>GET /api/transactions</c> against Postgres: the admin-wide, paginated transaction ledger.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ListTransactionsTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A transaction just created appears in the admin's list, named after its client.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListIncludesAFreshlyCreatedTransaction()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var transactionId = await BillingTestSeeds.CreateTransactionAsync(admin, clientId, 8m);

        var response = await admin.GetAsync("/api/transactions?page=1&pageSize=100");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("transactions").GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetInt32()).Should().Contain(transactionId);
        body.RootElement.GetProperty("transactions").GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("id").GetInt32() == transactionId)
            .GetProperty("clientName").GetString().Should().Be("Ada Lovelace");
    }

    /// <summary>
    /// The totals add up every client's ledger in the base currency: a row in a currency worth half the base adds half
    /// its amount, and the answer names the base currency the tiles are in. Before, the global list reported zero.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TotalsAcrossAllClientsAreInTheBaseCurrency()
    {
        await BillingTestSeeds.EnsureCurrencyAsync(factory, "ZQT", "998", 0.5m);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var before = await PagedListReads.PageAsync(admin, "/api/transactions?page=1&pageSize=1");
        var baseCode = before.GetProperty("currency").GetString()!;

        await SeedTransactionAsync(clientId, 100m, 10m, "ZQT");
        await SeedTransactionAsync(clientId, 10m, 0m, baseCode);
        var after = await PagedListReads.PageAsync(admin, "/api/transactions?page=1&pageSize=1");

        (after.GetProperty("totalIn").GetDecimal() - before.GetProperty("totalIn").GetDecimal()).Should().Be(60m);
        (after.GetProperty("totalFees").GetDecimal() - before.GetProperty("totalFees").GetDecimal()).Should().Be(5m);
        (after.GetProperty("balance").GetDecimal() - before.GetProperty("balance").GetDecimal()).Should().Be(55m);
    }

    /// <summary>A transaction recorded by hand with no invoice is in the currency its client is billed in.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ManualTransactionTakesTheClientsCurrency()
    {
        await BillingTestSeeds.EnsureCurrencyAsync(factory, "ZQT", "998", 0.5m);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory, "ZQT");
        var transactionId = await BillingTestSeeds.CreateTransactionAsync(admin, clientId, 8m);

        var fetched = await PagedListReads.PageAsync(admin, $"/api/transactions/{transactionId}");

        fetched.GetProperty("currency").GetString().Should().Be("ZQT");
    }

    /// <summary>A transaction recorded against an invoice is in the invoice's currency, whatever the client is billed in now.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TransactionOfAnInvoiceTakesTheInvoiceCurrency()
    {
        await BillingTestSeeds.EnsureCurrencyAsync(factory, "ZQT", "998", 0.5m);
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var invoiceId = await SeedInvoiceAsync(clientId, "ZQT");

        var created = await admin.PostAsJsonAsync("/api/transactions", new
        {
            clientId,
            date = TestMoments.Seeded,
            description = "Payment",
            transactionId = $"txn-{Guid.NewGuid():N}",
            invoiceId,
            paymentMethod = "manual",
            amountIn = 20m,
            amountOut = 0m,
            fees = 0m,
            addToCredit = false,
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var transactionId = await created.Content.ReadFromJsonAsync<int>();

        var fetched = await PagedListReads.PageAsync(admin, $"/api/transactions/{transactionId}");

        fetched.GetProperty("currency").GetString().Should().Be("ZQT");
    }

    /// <summary>The transaction endpoints are not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ListRefusesAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/api/transactions?page=1&pageSize=20");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>The search matches the description, the reference and the payment method in any letter case, and the id.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchFindsDescriptionReferenceMethodAndId()
    {
        var (admin, clientId, currency) = await SeedLedgerClientAsync();
        var token = Guid.NewGuid().ToString("N")[..10];
        var byDescription = await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, TestMoments.Seeded, $"Renewal {token}");
        var byReference = await TransactionListTestSeeds.SeedAsync(
            factory, clientId, currency, TestMoments.Seeded, "Plain", reference: $"ref-{token}");
        var byMethod = await TransactionListTestSeeds.SeedAsync(
            factory, clientId, currency, TestMoments.Seeded, "Plain", paymentMethod: $"gateway-{token}");
        var unrelated = await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, TestMoments.Seeded, "Unrelated");

        var tokenPage = await PagedListReads.PageAsync(admin, $"/api/transactions?page=1&pageSize=100&search={token.ToUpperInvariant()}");
        var idPage = await PagedListReads.PageAsync(admin, $"/api/transactions?page=1&pageSize=100&clientId={clientId}&search={unrelated}");

        IdsOf(tokenPage).Should().BeEquivalentTo([byDescription, byReference, byMethod]);
        IdsOf(idPage).Should().Contain(unrelated);
    }

    /// <summary>A percent sign and an underscore typed into the search match themselves, not any text.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchWildcardsAreLiteral()
    {
        var (admin, clientId, currency) = await SeedLedgerClientAsync();
        var token = Guid.NewGuid().ToString("N")[..10];
        var percent = await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, TestMoments.Seeded, $"{token} 50% off");
        await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, TestMoments.Seeded, $"{token} 50x off");
        var underscore = await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, TestMoments.Seeded, $"{token} a_c");
        await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, TestMoments.Seeded, $"{token} abc");

        var percentPage = await PagedListReads.PageAsync(admin, $"/api/transactions?page=1&pageSize=100&search={token}%2050%25");
        var underscorePage = await PagedListReads.PageAsync(admin, $"/api/transactions?page=1&pageSize=100&search={token}%20a_c");
        var bareWildcard = await PagedListReads.PageAsync(admin, $"/api/transactions?page=1&pageSize=100&search={token}%25%25");

        IdsOf(percentPage).Should().Equal(percent);
        IdsOf(underscorePage).Should().Equal(underscore);
        IdsOf(bareWildcard).Should().BeEmpty();
    }

    /// <summary>The client filter keeps only that client's rows, on the global list as well as on the client route.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ClientFilterKeepsOnlyThatClientsRows()
    {
        var (admin, first, currency) = await SeedLedgerClientAsync();
        var second = await BillingTestSeeds.SeedClientAsync(factory);
        var firstRow = await TransactionListTestSeeds.SeedAsync(factory, first, currency, TestMoments.Seeded, "First");
        var secondRow = await TransactionListTestSeeds.SeedAsync(factory, second, currency, TestMoments.Seeded, "Second");

        var byFilter = await PagedListReads.PageAsync(admin, $"/api/transactions?page=1&pageSize=100&clientId={first}");
        var byRoute = await PagedListReads.PageAsync(admin, $"/api/transactions/client/{second}?page=1&pageSize=100&clientId={first}");

        IdsOf(byFilter).Should().Equal(firstRow);
        IdsOf(byRoute).Should().Equal(secondRow);
    }

    /// <summary>The direction filter keeps the entries that took money in, or the ones that sent money out.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task DirectionFilterKeepsMoneyInOrOut()
    {
        var (admin, clientId, currency) = await SeedLedgerClientAsync();
        var received = await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, TestMoments.Seeded, "Received", amountIn: 30m);
        var sent = await TransactionListTestSeeds.SeedAsync(
            factory, clientId, currency, TestMoments.Seeded, "Sent", amountIn: 0m, amountOut: 12m);

        var inPage = await PagedListReads.PageAsync(admin, $"/api/transactions?page=1&pageSize=100&clientId={clientId}&direction=In");
        var outPage = await PagedListReads.PageAsync(admin, $"/api/transactions?page=1&pageSize=100&clientId={clientId}&direction=Out");

        IdsOf(inPage).Should().Equal(received);
        IdsOf(outPage).Should().Equal(sent);
    }

    /// <summary>The payment method filter keeps the rows of exactly that method, not the ones that merely contain it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PaymentMethodFilterMatchesTheWholeMethod()
    {
        var (admin, clientId, currency) = await SeedLedgerClientAsync();
        var card = await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, TestMoments.Seeded, "Card", paymentMethod: "card");
        await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, TestMoments.Seeded, "Prepaid", paymentMethod: "card-prepaid");
        var manual = await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, TestMoments.Seeded, "Manual", paymentMethod: "manual");

        var cardPage = await PagedListReads.PageAsync(admin, $"/api/transactions?page=1&pageSize=100&clientId={clientId}&paymentMethod=card");
        var manualPage = await PagedListReads.PageAsync(admin, $"/api/transactions?page=1&pageSize=100&clientId={clientId}&paymentMethod=manual");

        IdsOf(cardPage).Should().Equal(card);
        IdsOf(manualPage).Should().Equal(manual);
    }

    /// <summary>The period keeps the whole of its first and last day, and nothing outside them.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PeriodKeepsTheWholeLastDay()
    {
        var (admin, clientId, currency) = await SeedLedgerClientAsync();
        var day = new DateTimeOffset(2026, 3, 10, 0, 0, 0, TimeSpan.Zero);
        await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, day.AddSeconds(-1), "Before");
        var first = await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, day, "First instant");
        var last = await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, day.AddDays(1).AddSeconds(-1), "Last second");
        await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, day.AddDays(1), "After");

        var oneDay = await PagedListReads.PageAsync(
            admin, $"/api/transactions?page=1&pageSize=100&clientId={clientId}&from=2026-03-10&to=2026-03-10");
        var fromOnly = await PagedListReads.PageAsync(admin, $"/api/transactions?page=1&pageSize=100&clientId={clientId}&from=2026-03-10");
        var toOnly = await PagedListReads.PageAsync(admin, $"/api/transactions?page=1&pageSize=100&clientId={clientId}&to=2026-03-09");

        IdsOf(oneDay).Should().BeEquivalentTo([first, last]);
        IdsOf(fromOnly).Should().HaveCount(3);
        IdsOf(toOnly).Should().HaveCount(1);
    }

    /// <summary>The amount filter keeps the rows whose amount in or amount out is exactly that amount.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AmountFilterMatchesAmountInOrAmountOut()
    {
        var (admin, clientId, currency) = await SeedLedgerClientAsync();
        var received = await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, TestMoments.Seeded, "Received", amountIn: 40m);
        var sent = await TransactionListTestSeeds.SeedAsync(
            factory, clientId, currency, TestMoments.Seeded, "Sent", amountIn: 0m, amountOut: 40m);
        await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, TestMoments.Seeded, "Other", amountIn: 41m);

        var page = await PagedListReads.PageAsync(admin, $"/api/transactions?page=1&pageSize=100&clientId={clientId}&amount=40");

        IdsOf(page).Should().BeEquivalentTo([received, sent]);
    }

    /// <summary>The search and every filter narrow together: a row must pass each one.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CombinedFiltersAllApply()
    {
        var (admin, clientId, currency) = await SeedLedgerClientAsync();
        var other = await BillingTestSeeds.SeedClientAsync(factory);
        var token = Guid.NewGuid().ToString("N")[..10];
        var match = await TransactionListTestSeeds.SeedAsync(
            factory, clientId, currency, TestMoments.Seeded, $"Invoice {token}", paymentMethod: "card", amountIn: 25m);
        await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, TestMoments.Seeded, $"Invoice {token}", paymentMethod: "manual", amountIn: 25m);
        await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, TestMoments.Seeded, $"Invoice {token}", paymentMethod: "card", amountIn: 26m);
        await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, TestMoments.Seeded, $"Other {token}", paymentMethod: "card", amountIn: 25m);
        await TransactionListTestSeeds.SeedAsync(factory, other, currency, TestMoments.Seeded, $"Invoice {token}", paymentMethod: "card", amountIn: 25m);

        var page = await PagedListReads.PageAsync(
            admin,
            $"/api/transactions?page=1&pageSize=100&search=invoice%20{token}&clientId={clientId}&direction=In&paymentMethod=card&amount=25&from=2026-03-10&to=2026-03-10");

        IdsOf(page).Should().Equal(match);
    }

    /// <summary>The totals add up the rows the filters keep, not the whole ledger, and a credit-only row counts in neither.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TotalsFollowTheFilters()
    {
        var (admin, clientId, currency) = await SeedLedgerClientAsync();
        await TransactionListTestSeeds.SeedAsync(
            factory, clientId, currency, TestMoments.Seeded, "Card payment", paymentMethod: "card", amountIn: 100m, fees: 10m);
        await TransactionListTestSeeds.SeedAsync(
            factory, clientId, currency, TestMoments.Seeded, "Refund", paymentMethod: "manual", amountIn: 0m, amountOut: 40m);
        await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, TestMoments.Seeded, "Manual payment", paymentMethod: "manual", amountIn: 5m);
        await TransactionListTestSeeds.SeedCreditOnlyAsync(factory, clientId, currency, TestMoments.Seeded, "Goodwill credit");
        var route = $"/api/transactions?page=1&pageSize=100&clientId={clientId}";

        var everything = await PagedListReads.PageAsync(admin, route);
        var card = await PagedListReads.PageAsync(admin, $"{route}&paymentMethod=card");
        var received = await PagedListReads.PageAsync(admin, $"{route}&direction=In");
        var sent = await PagedListReads.PageAsync(admin, $"{route}&direction=Out");
        var none = await PagedListReads.PageAsync(admin, $"{route}&search=nothing-{Guid.NewGuid():N}");

        AssertTotals(everything, totalIn: 105m, totalOut: 40m, totalFees: 10m, balance: 55m, count: 3);
        AssertTotals(card, totalIn: 100m, totalOut: 0m, totalFees: 10m, balance: 90m, count: 1);
        AssertTotals(received, totalIn: 105m, totalOut: 0m, totalFees: 10m, balance: 95m, count: 2);
        AssertTotals(sent, totalIn: 0m, totalOut: 40m, totalFees: 0m, balance: -40m, count: 1);
        AssertTotals(none, totalIn: 0m, totalOut: 0m, totalFees: 0m, balance: 0m, count: 0);
    }

    /// <summary>A credit-only row is never listed, whatever the filters ask for.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task CreditOnlyRowsStayExcluded()
    {
        var (admin, clientId, currency) = await SeedLedgerClientAsync();
        var token = Guid.NewGuid().ToString("N")[..10];
        var cash = await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, TestMoments.Seeded, $"Cash {token}");
        var credit = await TransactionListTestSeeds.SeedCreditOnlyAsync(factory, clientId, currency, TestMoments.Seeded, $"Credit {token}");

        var searched = await PagedListReads.PageAsync(admin, $"/api/transactions?page=1&pageSize=100&search={token}");
        var byId = await PagedListReads.PageAsync(admin, $"/api/transactions?page=1&pageSize=100&search={credit}&clientId={clientId}");

        IdsOf(searched).Should().Equal(cash);
        IdsOf(byId).Should().NotContain(credit);
    }

    /// <summary>A filter is applied before paging: the total counts every match, each page holds its slice, newest first and the id breaks a tie.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task FilterIsAppliedBeforePaging()
    {
        var (admin, clientId, currency) = await SeedLedgerClientAsync();
        var oldest = await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, TestMoments.Seeded, "Oldest");
        var tiedFirst = await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, TestMoments.Seeded.AddDays(1), "Tied first");
        var tiedSecond = await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, TestMoments.Seeded.AddDays(1), "Tied second");
        var newest = await TransactionListTestSeeds.SeedAsync(factory, clientId, currency, TestMoments.Seeded.AddDays(2), "Newest");
        var route = $"/api/transactions?pageSize=3&clientId={clientId}";

        var first = await PagedListReads.PageAsync(admin, $"{route}&page=1");
        var second = await PagedListReads.PageAsync(admin, $"{route}&page=2");
        var beyond = await PagedListReads.PageAsync(admin, $"{route}&page=3");

        IdsOf(first).Should().Equal(newest, tiedSecond, tiedFirst);
        first.GetProperty("transactions").GetProperty("totalCount").GetInt32().Should().Be(4);
        IdsOf(second).Should().Equal(oldest);
        IdsOf(beyond).Should().BeEmpty();
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

        var response = await admin.GetAsync($"/api/transactions?{queryString}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PagedListReads.RefusedFieldsAsync(response)).Should().Contain(field);
    }

    /// <summary>A search longer than the limit is refused with 400 on the search, not cut.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task SearchOverTheLimitIsRefused()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/transactions?page=1&pageSize=20&search={new string('a', PagingLimits.MaxSearchLength + 1)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PagedListReads.RefusedFieldsAsync(response)).Should().Contain("search");
    }

    /// <summary>A client id, an amount or a payment method the filters cannot honour is refused with 400 on that field.</summary>
    /// <param name="filter">The filter's key and value.</param>
    /// <param name="field">The field the refusal must name.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("clientId=0", "clientid")]
    [InlineData("clientId=-4", "clientid")]
    [InlineData("amount=0", "amount")]
    [InlineData("amount=-5", "amount")]
    [InlineData("from=2026-03-11&to=2026-03-10", "to")]
    public async Task FilterValueOutOfRangeIsRefused(string filter, string field)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/transactions?page=1&pageSize=20&{filter}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PagedListReads.RefusedFieldsAsync(response)).Should().Contain(field);
    }

    /// <summary>A payment method longer than a stored one can be is refused with 400 on the payment method.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PaymentMethodOverTheLimitIsRefused()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/transactions?page=1&pageSize=20&paymentMethod={new string('m', 101)}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await PagedListReads.RefusedFieldsAsync(response)).Should().Contain("paymentmethod");
    }

    /// <summary>
    /// A direction that names nothing, by name or by number, is refused with 400: the model binder turns it back before the
    /// validator runs, so the refusal is the validation code without a field of the validator's own.
    /// </summary>
    /// <param name="filter">The filter's key and value.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("direction=Sideways")]
    [InlineData("direction=99")]
    public async Task UnknownDirectionIsRefused(string filter)
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);

        var response = await admin.GetAsync($"/api/transactions?page=1&pageSize=20&{filter}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await BillingTestSeeds.CodeOfAsync(response)).Should().Be(SharedErrorCodes.ValidationError);
    }

    /// <summary>Signs in an admin, seeds a client and reads the base currency the ledger totals are in.</summary>
    /// <returns>The admin client, the new client's id and the base currency code to seed rows in.</returns>
    private async Task<(HttpClient Admin, int ClientId, string Currency)> SeedLedgerClientAsync()
    {
        var admin = await BillingTestSeeds.AdminClientAsync(factory);
        var clientId = await BillingTestSeeds.SeedClientAsync(factory);
        var empty = await PagedListReads.PageAsync(admin, $"/api/transactions?page=1&pageSize=1&clientId={clientId}");
        return (admin, clientId, empty.GetProperty("currency").GetString()!);
    }

    /// <summary>Reads the ids of the rows of a transaction list answer, in the order the page holds them.</summary>
    /// <param name="result">The answer body.</param>
    /// <returns>The row ids.</returns>
    private static List<int> IdsOf(JsonElement result) =>
        PagedListReads.IdsOf(result.GetProperty("transactions"));

    /// <summary>Asserts the summary totals and the number of rows an answer reports.</summary>
    /// <param name="result">The answer body.</param>
    /// <param name="totalIn">The expected total in.</param>
    /// <param name="totalOut">The expected total out.</param>
    /// <param name="totalFees">The expected total fees.</param>
    /// <param name="balance">The expected balance.</param>
    /// <param name="count">The expected number of rows across all pages.</param>
    private static void AssertTotals(JsonElement result, decimal totalIn, decimal totalOut, decimal totalFees, decimal balance, int count)
    {
        result.GetProperty("totalIn").GetDecimal().Should().Be(totalIn);
        result.GetProperty("totalOut").GetDecimal().Should().Be(totalOut);
        result.GetProperty("totalFees").GetDecimal().Should().Be(totalFees);
        result.GetProperty("balance").GetDecimal().Should().Be(balance);
        result.GetProperty("transactions").GetProperty("totalCount").GetInt32().Should().Be(count);
    }

    /// <summary>Writes a ledger row straight into the database, in the given currency.</summary>
    /// <param name="clientId">The client the row belongs to.</param>
    /// <param name="amountIn">The amount credited.</param>
    /// <param name="fees">The fees charged.</param>
    /// <param name="currency">ISO 4217 code the amounts are in.</param>
    /// <returns>A task that completes when the work is done.</returns>
    private async Task SeedTransactionAsync(int clientId, decimal amountIn, decimal fees, string currency)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        context.Transactions.Add(Transaction.Create(
            clientId, DateTimeOffset.UtcNow, "Totals test", $"tx-{Guid.NewGuid():N}", null, "manual",
            amountIn, 0m, fees, false, currency, TransactionSource.Manual));
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Writes an unpaid invoice straight into the database, in the given currency.</summary>
    /// <param name="clientId">The client the invoice is for.</param>
    /// <param name="currency">ISO 4217 code the invoice bills in.</param>
    /// <returns>The new invoice's primary key.</returns>
    private async Task<int> SeedInvoiceAsync(int clientId, string currency)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(14), currency, DateTimeOffset.UtcNow);
        invoice.AddItem("Hosting", 20m, 1);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync(CancellationToken.None);
        return invoice.Id;
    }
}
