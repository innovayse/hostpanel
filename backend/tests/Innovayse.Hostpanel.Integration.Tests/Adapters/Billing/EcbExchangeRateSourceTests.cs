using System.Net;
using Innovayse.Hostpanel.Application.Billing.Currencies.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.Currency;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Billing;

/// <summary>
/// The European Central Bank publishes "1 EUR = X units" in an XML document; the source turns that
/// into "1 unit = X base" for whatever the base is, over a real client aimed at a stub bank server.
/// </summary>
public sealed class EcbExchangeRateSourceTests : IAsyncLifetime
{
    /// <summary>How many US dollars the stub bank says one euro buys.</summary>
    private const decimal UsdPerEur = 1.25m;

    /// <summary>How many pounds the stub bank says one euro buys.</summary>
    private const decimal GbpPerEur = 0.85m;

    /// <summary>The precision the arithmetic is compared at.</summary>
    private const decimal Tolerance = 0.0000001m;

    /// <summary>The loopback server standing in for the bank's site; started per test class instance.</summary>
    private StubHttpServer http = null!;

    /// <summary>Starts the stub bank server before each test.</summary>
    /// <returns>A task that completes when the server listens.</returns>
    public async Task InitializeAsync()
    {
        http = await StubHttpServer.StartAsync();
    }

    /// <summary>Stops the stub bank server after each test.</summary>
    /// <returns>A task that completes when the server is down.</returns>
    public async Task DisposeAsync()
    {
        await http.DisposeAsync();
    }

    /// <summary>Builds the source over a client aimed at the stub server.</summary>
    /// <param name="timeout">How long the client waits for an answer.</param>
    /// <returns>The source under test.</returns>
    private EcbExchangeRateSource Source(TimeSpan? timeout = null) =>
        new(
            new HttpClient { BaseAddress = http.BaseAddress, Timeout = timeout ?? TimeSpan.FromSeconds(30) },
            NullLogger<EcbExchangeRateSource>.Instance);

    /// <summary>Asks the source under test for a set of codes against a base.</summary>
    /// <param name="baseCode">The base currency.</param>
    /// <param name="codes">The codes to quote.</param>
    /// <returns>The source's answer.</returns>
    private Task<Result<ExchangeRateQuotes>> QuoteAsync(string baseCode, params string[] codes) =>
        Source().QuoteAsync(new ExchangeRateLookup(baseCode, codes), CancellationToken.None);

    /// <summary>With the euro as the base, one unit of a currency is the inverse of what one euro buys.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task QuoteAsync_WithEuroBaseInvertsTheQuotes()
    {
        http.Enqueue(HttpStatusCode.OK, EcbReplies.Daily, EcbReplies.ContentType);

        var rates = (await QuoteAsync("EUR", "USD", "GBP")).Value!.RatesToBase;

        rates["USD"].Should().BeApproximately(1m / UsdPerEur, Tolerance);
        rates["GBP"].Should().BeApproximately(1m / GbpPerEur, Tolerance);
        http.Requests.Should().ContainSingle().Which.PathAndQuery.Should().EndWith("stats/eurofxref/eurofxref-daily.xml");
    }

    /// <summary>With a base other than the euro, the quotes are re-expressed against it, the euro included.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task QuoteAsync_WithAnotherBaseConvertsThroughTheEuro()
    {
        http.Enqueue(HttpStatusCode.OK, EcbReplies.Daily, EcbReplies.ContentType);

        var rates = (await QuoteAsync("USD", "EUR", "GBP")).Value!.RatesToBase;

        rates["EUR"].Should().BeApproximately(UsdPerEur, Tolerance);
        rates["GBP"].Should().BeApproximately(UsdPerEur / GbpPerEur, Tolerance);
    }

    /// <summary>Codes and bases are matched without regard to case.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task QuoteAsync_IgnoresTheCaseOfCodes()
    {
        http.Enqueue(HttpStatusCode.OK, EcbReplies.Daily, EcbReplies.ContentType);

        var rates = (await QuoteAsync("usd", "eur", "gbp")).Value!.RatesToBase;

        rates.Keys.Should().BeEquivalentTo(["EUR", "GBP"]);
    }

    /// <summary>A currency the bank does not publish, or publishes with a rate that is no number or not above zero, is absent.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task QuoteAsync_OmitsCurrenciesTheBankDoesNotQuote()
    {
        http.Enqueue(
            HttpStatusCode.OK,
            EcbReplies.Document("2026-11-13", ("USD", "1.25"), ("GBP", "n/a"), ("JPY", "0")),
            EcbReplies.ContentType);

        var rates = (await QuoteAsync("EUR", "USD", "AMD", "GBP", "JPY", "ZZZ")).Value!.RatesToBase;

        rates.Keys.Should().BeEquivalentTo(["USD"]);
    }

    /// <summary>A base the bank does not quote, the dram for one, cannot anchor a conversion; the whole call refuses.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task QuoteAsync_RefusesABaseTheBankDoesNotQuote()
    {
        http.Enqueue(HttpStatusCode.OK, EcbReplies.Daily, EcbReplies.ContentType);

        var result = await QuoteAsync("AMD", "USD");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.EXCHANGE_RATE_BASE_NOT_QUOTED);
    }

    /// <summary>A non-success answer is the source being unavailable, not an empty rate table.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task QuoteAsync_FailedResponseIsUnavailable()
    {
        http.Enqueue(HttpStatusCode.BadGateway, "down", "text/plain");

        var result = await QuoteAsync("EUR", "USD");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.EXCHANGE_RATE_SOURCE_UNAVAILABLE);
    }

    /// <summary>A body that is not the published document, markup or not, is the source being unavailable too.</summary>
    /// <param name="body">What the stub bank answers with.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("<html><body>maintenance")]
    [InlineData("not xml at all")]
    [InlineData("")]
    [InlineData("<root><Cube/></root>")]
    public async Task QuoteAsync_UnreadableBodyIsUnavailable(string body)
    {
        http.Enqueue(HttpStatusCode.OK, body, EcbReplies.ContentType);

        var result = await QuoteAsync("EUR", "USD");

        result.ErrorCode.Should().Be(HostpanelErrorCodes.EXCHANGE_RATE_SOURCE_UNAVAILABLE);
    }

    /// <summary>A document that declares a DTD is refused unread, so no entity is ever expanded.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task QuoteAsync_DocumentWithADtdIsUnavailable()
    {
        var withDtd = EcbReplies.Daily.Replace(
            "<gesmes:Envelope", "<!DOCTYPE gesmes:Envelope [<!ENTITY x \"y\">]><gesmes:Envelope", StringComparison.Ordinal);
        http.Enqueue(HttpStatusCode.OK, withDtd, EcbReplies.ContentType);

        var result = await QuoteAsync("EUR", "USD");

        result.ErrorCode.Should().Be(HostpanelErrorCodes.EXCHANGE_RATE_SOURCE_UNAVAILABLE);
    }

    /// <summary>A set whose publication date is not a date, or is not written as one, is not the published document.</summary>
    /// <param name="date">What the stub bank writes as the day of the set.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData("")]
    [InlineData("yesterday")]
    [InlineData("2026-13-45")]
    [InlineData("13/11/2026")]
    public async Task QuoteAsync_UnreadableDateIsUnavailable(string date)
    {
        http.Enqueue(HttpStatusCode.OK, EcbReplies.Document(date, ("USD", "1.25")), EcbReplies.ContentType);

        var result = await QuoteAsync("EUR", "USD");

        result.ErrorCode.Should().Be(HostpanelErrorCodes.EXCHANGE_RATE_SOURCE_UNAVAILABLE);
    }

    /// <summary>A body past the size the feed reads is refused unread, as the source being unavailable.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task QuoteAsync_OversizedBodyIsUnavailable()
    {
        var padding = new string(' ', (int)ExchangeRateFeed.MaxBodyBytes);
        http.Enqueue(HttpStatusCode.OK, EcbReplies.Daily + padding, EcbReplies.ContentType);

        var result = await QuoteAsync("EUR", "USD");

        result.ErrorCode.Should().Be(HostpanelErrorCodes.EXCHANGE_RATE_SOURCE_UNAVAILABLE);
    }

    /// <summary>
    /// A rate so large that one unit rounds to nothing in the euro is an unusable feed when it is the base (it
    /// would be the divisor), and is left out when it is only a wanted currency.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task QuoteAsync_RateThatRoundsToZeroIsUnusable()
    {
        const string Huge = "30000000000000000000000000000";
        var document = EcbReplies.Document("2026-11-13", ("USD", "1.25"), ("JPY", Huge));
        http.Enqueue(HttpStatusCode.OK, document, EcbReplies.ContentType);
        http.Enqueue(HttpStatusCode.OK, document, EcbReplies.ContentType);

        var asBase = await QuoteAsync("JPY", "USD");
        var asCode = await QuoteAsync("EUR", "USD", "JPY");

        asBase.ErrorCode.Should().Be(HostpanelErrorCodes.EXCHANGE_RATE_SOURCE_UNAVAILABLE);
        asCode.Value!.RatesToBase.Keys.Should().BeEquivalentTo(["USD"]);
    }

    /// <summary>A bank that holds the request past the client's timeout is the source being unavailable.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task QuoteAsync_TimeoutIsUnavailable()
    {
        http.Enqueue(new StubReply(HttpStatusCode.OK, EcbReplies.Daily, EcbReplies.ContentType, Delay: TimeSpan.FromSeconds(30)));

        var result = await Source(TimeSpan.FromMilliseconds(300))
            .QuoteAsync(new ExchangeRateLookup("EUR", ["USD"]), CancellationToken.None);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.EXCHANGE_RATE_SOURCE_UNAVAILABLE);
    }
}
