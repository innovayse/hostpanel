using System.Net;
using Innovayse.Hostpanel.Application.Billing.Currencies.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Infrastructure.Currency;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Billing;

/// <summary>
/// The Central Bank of Armenia publishes "1 unit = X AMD" as strings; the source turns that
/// into "1 unit = X base" for whatever the base is, without touching the network.
/// </summary>
public sealed class CbaExchangeRateSourceTests : IAsyncLifetime
{
    /// <summary>A trimmed copy of what <c>latest.json.php</c> answered on 2026-09-13: strings, and null for unquoted codes.</summary>
    private const string Quotes =
        """{"AED":"98.906","ARS":null,"EUR":"421.11","GBP":"490.61","RUB":"4.2971","USD":"363.28","XAU":"50987"}""";

    /// <summary>The quoted AMD price of one US dollar.</summary>
    private const decimal AmdPerUsd = 363.28m;

    /// <summary>The quoted AMD price of one euro.</summary>
    private const decimal AmdPerEur = 421.11m;

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
    /// <returns>The source under test.</returns>
    private CbaExchangeRateSource Source() =>
        new(new HttpClient { BaseAddress = http.BaseAddress }, NullLogger<CbaExchangeRateSource>.Instance);

    /// <summary>Asks the source under test for a set of codes against a base.</summary>
    /// <param name="baseCode">The base currency.</param>
    /// <param name="codes">The codes to quote.</param>
    /// <returns>The source's answer.</returns>
    private Task<Result<ExchangeRateQuotes>> QuoteAsync(string baseCode, params string[] codes) =>
        Source().QuoteAsync(new ExchangeRateLookup(baseCode, codes), CancellationToken.None);

    /// <summary>With base USD, one AMD is 1/363.28 USD and one EUR is 421.11/363.28 USD.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task QuoteAsync_ConvertsQuotesToTheBase()
    {
        http.Enqueue(Quotes);

        var rates = (await QuoteAsync("USD", "AMD", "EUR")).Value!.RatesToBase;

        rates.Should().HaveCount(2);
        rates["AMD"].Should().BeApproximately(1m / AmdPerUsd, 0.0000001m);
        rates["EUR"].Should().BeApproximately(AmdPerEur / AmdPerUsd, 0.0000001m);
        http.Requests.Should().ContainSingle().Which.PathAndQuery.Should().EndWith("latest.json.php");
    }

    /// <summary>With base AMD, the quotes are the answer as published.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task QuoteAsync_WithAmdBaseReadsQuotesDirectly()
    {
        http.Enqueue(Quotes);

        var rates = (await QuoteAsync("AMD", "USD", "EUR")).Value!.RatesToBase;

        rates["USD"].Should().Be(AmdPerUsd);
        rates["EUR"].Should().Be(AmdPerEur);
    }

    /// <summary>
    /// A quote given per 100 units is divided down to one unit: "100 JPY = 243 AMD" is 2.43 AMD per
    /// yen, never 243.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task QuoteAsync_HonoursTheQuotedUnitAmount()
    {
        http.Enqueue("""{"USD":"363.28","JPY":{"rate":"243","amount":100},"IRR":{"Rate":"0.95","Amount":"10"}}""");

        var rates = (await QuoteAsync("AMD", "JPY", "IRR", "USD")).Value!.RatesToBase;

        rates["JPY"].Should().Be(2.43m);
        rates["IRR"].Should().Be(0.095m);
        rates["USD"].Should().Be(AmdPerUsd);
    }

    /// <summary>A code the bank does not quote, or quotes as null, is simply absent from the answer.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task QuoteAsync_OmitsUnquotedCodes()
    {
        http.Enqueue(Quotes);

        var rates = (await QuoteAsync("usd", "ars", "zzz", "gbp")).Value!.RatesToBase;

        rates.Keys.Should().BeEquivalentTo(["GBP"]);
    }

    /// <summary>A base the bank does not quote cannot anchor a conversion; the whole call refuses.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task QuoteAsync_RefusesAnUnquotedBase()
    {
        http.Enqueue(Quotes);

        var result = await QuoteAsync("ZZZ", "USD");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.EXCHANGE_RATE_BASE_NOT_QUOTED);
    }

    /// <summary>A non-success answer is the source being unavailable, not an empty rate table.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task QuoteAsync_FailedResponseIsUnavailable()
    {
        http.Enqueue(HttpStatusCode.BadGateway, "down");

        var result = await QuoteAsync("USD", "AMD");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be(HostpanelErrorCodes.EXCHANGE_RATE_SOURCE_UNAVAILABLE);
    }

    /// <summary>A body that is not the published object is the source being unavailable too.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task QuoteAsync_UnreadableBodyIsUnavailable()
    {
        http.Enqueue("<html>maintenance</html>");

        var result = await QuoteAsync("USD", "AMD");

        result.ErrorCode.Should().Be(HostpanelErrorCodes.EXCHANGE_RATE_SOURCE_UNAVAILABLE);
    }
}
