using System.Diagnostics;
using System.Net;
using Innovayse.Hostpanel.Application.Billing.Currencies.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings.Enums;
using Innovayse.Hostpanel.Infrastructure.Currency;
using Innovayse.Hostpanel.Infrastructure.Currency.Extensions;
using Innovayse.Hostpanel.Infrastructure.Resilience;
using Innovayse.Hostpanel.Infrastructure.Resilience.Settings;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Adapters.Billing;

/// <summary>
/// What the download both banks share does with a bad answer, over the application's own client registration
/// (the origin, the body cap and the read only resilience profile) aimed at a stub bank server: a body past the
/// cap is refused unread and a body that stalls is cut by the client's own timeout, each as the source being unavailable.
/// </summary>
public sealed class ExchangeRateFeedTests : IAsyncLifetime
{
    /// <summary>A bulletin of the Central Bank of Armenia that the source reads without complaint.</summary>
    private const string CbaBulletin = """{"USD":"400"}""";

    /// <summary>How long the stub holds a body back, far past the client's backstop timeout.</summary>
    private static readonly TimeSpan StalledBody = TimeSpan.FromSeconds(60);

    /// <summary>The time past the client's backstop the test allows for the call to unwind.</summary>
    private static readonly TimeSpan Slack = TimeSpan.FromSeconds(10);

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

    /// <summary>A body past the size the feed reads is refused unread, as the source being unavailable.</summary>
    /// <param name="provider">The bank whose source reads the answer.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(ExchangeRateProvider.Cba)]
    [InlineData(ExchangeRateProvider.Ecb)]
    public async Task QuoteAsync_OversizedBodyIsUnavailable(ExchangeRateProvider provider)
    {
        var padding = new string(' ', (int)ExchangeRateFeed.MaxBodyBytes);
        http.Enqueue(HttpStatusCode.OK, ValidBody(provider) + padding, ContentType(provider));

        var result = await QuoteAsync(provider);

        result.ErrorCode.Should().Be(HostpanelErrorCodes.EXCHANGE_RATE_SOURCE_UNAVAILABLE);
    }

    /// <summary>A body that stalls after the headers is cut by the client's backstop timeout: the resilience pipeline ends with the headers and does not cover it.</summary>
    /// <param name="provider">The bank whose source reads the answer.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(ExchangeRateProvider.Cba)]
    [InlineData(ExchangeRateProvider.Ecb)]
    public async Task QuoteAsync_StalledBodyIsUnavailableWithinTheBackstopTimeout(ExchangeRateProvider provider)
    {
        http.Enqueue(new StubReply(HttpStatusCode.OK, ValidBody(provider), ContentType(provider), BodyDelay: StalledBody));
        var clock = Stopwatch.StartNew();

        var result = await QuoteAsync(provider);

        clock.Elapsed.Should().BeLessThan(HttpBackstopTimeouts.ExchangeRate + Slack);
        result.ErrorCode.Should().Be(HostpanelErrorCodes.EXCHANGE_RATE_SOURCE_UNAVAILABLE);
    }

    /// <summary>A body inside the cap is read whole and quoted.</summary>
    /// <param name="provider">The bank whose source reads the answer.</param>
    /// <returns>A task that completes when the test has run.</returns>
    [Theory]
    [InlineData(ExchangeRateProvider.Cba)]
    [InlineData(ExchangeRateProvider.Ecb)]
    public async Task QuoteAsync_BodyInsideTheCapIsQuoted(ExchangeRateProvider provider)
    {
        http.Enqueue(HttpStatusCode.OK, ValidBody(provider), ContentType(provider));

        var result = await QuoteAsync(provider);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RatesToBase.Should().ContainKey("USD");
    }

    /// <summary>The body the stub bank answers with when the test wants a good set of rates.</summary>
    /// <param name="provider">The bank being imitated.</param>
    /// <returns>A bulletin or document that quotes the US dollar.</returns>
    private static string ValidBody(ExchangeRateProvider provider) =>
        provider == ExchangeRateProvider.Cba ? CbaBulletin : EcbReplies.Daily;

    /// <summary>The media type the stub bank answers with.</summary>
    /// <param name="provider">The bank being imitated.</param>
    /// <returns>The media type its feed is served as.</returns>
    private static string ContentType(ExchangeRateProvider provider) =>
        provider == ExchangeRateProvider.Cba ? "application/json" : EcbReplies.ContentType;

    /// <summary>Asks the bank's source, built by the application's registration with a short profile, for the US dollar against the dram or the euro.</summary>
    /// <param name="provider">The bank to ask.</param>
    /// <returns>The source's answer.</returns>
    private async Task<Result<ExchangeRateQuotes>> QuoteAsync(ExchangeRateProvider provider)
    {
        var shortProfile = new ResilienceProfileOptions
        {
            AttemptTimeout = TimeSpan.FromMilliseconds(500),
            TotalTimeout = TimeSpan.FromSeconds(2),
            MaxRetryAttempts = 0,
            CircuitBreakerEnabled = false,
        };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<HttpResilienceOptions>().Configure(o =>
        {
            o.Cba = shortProfile;
            o.Ecb = shortProfile;
        });
        services.AddExchangeRateHttpClients();
        services.AddHttpClient<CbaExchangeRateSource>().ConfigureHttpClient(c => c.BaseAddress = http.BaseAddress);
        services.AddHttpClient<EcbExchangeRateSource>().ConfigureHttpClient(c => c.BaseAddress = http.BaseAddress);
        await using var container = services.BuildServiceProvider();

        IExchangeRateSource source = provider == ExchangeRateProvider.Cba
            ? container.GetRequiredService<CbaExchangeRateSource>()
            : container.GetRequiredService<EcbExchangeRateSource>();

        return await source.QuoteAsync(new ExchangeRateLookup(provider, provider == ExchangeRateProvider.Cba ? "AMD" : "EUR", ["USD"]), CancellationToken.None);
    }
}
