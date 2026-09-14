namespace Innovayse.Infrastructure.Tests.Billing;

using System.Net;
using FluentAssertions;
using Innovayse.Infrastructure.Billing;
using Xunit;

/// <summary>
/// The Central Bank of Armenia publishes "1 unit = X AMD" as strings; the source turns that
/// into "1 unit = X base" for whatever the base is, without touching the network.
/// </summary>
public sealed class CbaExchangeRateSourceTests
{
    /// <summary>A trimmed copy of what <c>latest.json.php</c> answered on 2026-09-13: strings, and null for unquoted codes.</summary>
    private const string Quotes =
        """{"AED":"98.906","ARS":null,"EUR":"421.11","GBP":"490.61","RUB":"4.2971","USD":"363.28","XAU":"50987"}""";

    /// <summary>The quoted AMD price of one US dollar.</summary>
    private const decimal AmdPerUsd = 363.28m;

    /// <summary>The quoted AMD price of one euro.</summary>
    private const decimal AmdPerEur = 421.11m;

    /// <summary>The handler the source under test talks to.</summary>
    private readonly FakeHttpMessageHandler http = new();

    /// <summary>Builds the source over the fake handler.</summary>
    /// <returns>The source under test.</returns>
    private CbaExchangeRateSource Source() =>
        new(new HttpClient(http) { BaseAddress = new Uri("https://cb.am/") });

    /// <summary>With base USD, one AMD is 1/363.28 USD and one EUR is 421.11/363.28 USD.</summary>
    [Fact]
    public async Task RatesToAsync_ConvertsQuotesToTheBase()
    {
        http.Enqueue(Quotes);

        var rates = await Source().RatesToAsync("USD", ["AMD", "EUR"], CancellationToken.None);

        rates.Should().HaveCount(2);
        rates["AMD"].Should().BeApproximately(1m / AmdPerUsd, 0.0000001m);
        rates["EUR"].Should().BeApproximately(AmdPerEur / AmdPerUsd, 0.0000001m);
        http.Requests.Should().ContainSingle().Which.Should().EndWith("latest.json.php");
    }

    /// <summary>With base AMD, the quotes are the answer as published.</summary>
    [Fact]
    public async Task RatesToAsync_WithAmdBaseReadsQuotesDirectly()
    {
        http.Enqueue(Quotes);

        var rates = await Source().RatesToAsync("AMD", ["USD", "EUR"], CancellationToken.None);

        rates["USD"].Should().Be(AmdPerUsd);
        rates["EUR"].Should().Be(AmdPerEur);
    }

    /// <summary>A code the bank does not quote, or quotes as null, is simply absent from the answer.</summary>
    [Fact]
    public async Task RatesToAsync_OmitsUnquotedCodes()
    {
        http.Enqueue(Quotes);

        var rates = await Source().RatesToAsync("usd", ["ars", "zzz", "gbp"], CancellationToken.None);

        rates.Keys.Should().BeEquivalentTo(["GBP"]);
    }

    /// <summary>A base the bank does not quote cannot anchor a conversion; the whole call refuses.</summary>
    [Fact]
    public async Task RatesToAsync_RefusesAnUnquotedBase()
    {
        http.Enqueue(Quotes);

        var act = () => Source().RatesToAsync("ZZZ", ["USD"], CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ZZZ*");
    }

    /// <summary>A non-success answer is an error, not an empty rate table.</summary>
    [Fact]
    public async Task RatesToAsync_ThrowsOnAFailedResponse()
    {
        http.EnqueueStatus(HttpStatusCode.BadGateway, "down");

        var act = () => Source().RatesToAsync("USD", ["AMD"], CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
