using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Domains;
using Innovayse.Hostpanel.Domain.Domains.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Domains;

/// <summary>
/// Integration tests for the anonymous <c>GetTldPricing</c> use case:
/// <c>GET /api/domains/tld-pricing</c>, which the storefront and the client area both need
/// before or without a session.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class GetTldPricingTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>An anonymous caller reaches the endpoint and gets 200 with a currency block, even with nothing configured.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetTldPricing_AnonymousCaller_Returns200WithDefaultCurrencyAsync()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/domains/tld-pricing");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("currency").GetProperty("code").GetString().Should().Be("USD");
    }

    /// <summary>
    /// With no currency asked for and no base currency stored, the endpoint answers 503 with
    /// <c>BASE_CURRENCY_NOT_CONFIGURED</c> rather than a table in an empty currency code. Runs on a
    /// factory of its own, since removing the base currency would change every other test's database.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetTldPricing_NoBaseCurrency_Returns503BaseCurrencyNotConfiguredAsync()
    {
        await using var isolated = new IntegrationTestFactory();
        await isolated.InitializeAsync();
        await BillingTestSeeds.RemoveBaseCurrencyAsync(isolated);
        var client = isolated.CreateClient();

        var response = await client.GetAsync("/api/domains/tld-pricing");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("type").GetString().Should().Be("BASE_CURRENCY_NOT_CONFIGURED");
    }

    /// <summary>
    /// A currency that is not configured is refused with 404 <c>CURRENCY_NOT_FOUND</c> instead of a
    /// table of unconverted prices labelled with that code.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetTldPricing_UnconfiguredCurrency_Returns404CurrencyNotFoundAsync()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/domains/tld-pricing?currency=XXQ");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("type").GetString().Should().Be("CURRENCY_NOT_FOUND");
    }

    /// <summary>
    /// The configs kept in the cache between requests are not tracked by the data context that
    /// loaded them, so no tracked entity outlives its request.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetTldPricing_CachesUntrackedConfigsAsync()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/domains/tld-pricing?currency=USD");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var scope = factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        cache.TryGetValue("tld-pricing-configs", out List<TldConfig>? cached).Should().BeTrue();
        cached.Should().NotBeNull();
        foreach (var config in cached!)
        {
            ((DbContext)context).Entry(config).State.Should().Be(EntityState.Detached);
        }
    }

    /// <summary>An enabled TLD configuration is included in the pricing table, converted to the requested currency.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetTldPricing_IncludesAnEnabledTldConfigAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var tld = TldConfig.Create(
            $"pricing-{Guid.NewGuid():N}"[..12], RegistrarModule.NameAm, "USD", "USD",
            isEnabled: true, sortOrder: 0, categories: []);
        tld.UpdateCostPrices(
            register: new Dictionary<int, decimal> { [1] = 5m },
            transfer: new Dictionary<int, decimal> { [1] = 5m },
            renew: new Dictionary<int, decimal> { [1] = 5m });
        tld.UpdateSellPrices(
            register: new Dictionary<int, decimal> { [1] = 10m },
            transfer: new Dictionary<int, decimal> { [1] = 10m },
            renew: new Dictionary<int, decimal> { [1] = 10m });
        context.TldConfigs.Add(tld);
        await context.SaveChangesAsync(CancellationToken.None);

        var client = factory.CreateClient();
        var response = await client.GetAsync($"/api/domains/tld-pricing?currency=USD");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var entry = body.GetProperty("pricing").EnumerateArray().Single(e => e.GetProperty("tld").GetString() == tld.Tld);
        entry.GetProperty("sellCurrency").GetString().Should().Be("USD");
    }

    /// <summary>
    /// Each action's prices come as a list of typed period entries, shortest period first,
    /// whatever order the periods were stored in.
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetTldPricing_ListsEachPeriodPriceShortestFirstAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
        var tld = TldConfig.Create(
            $"period-{Guid.NewGuid():N}"[..12], RegistrarModule.NameAm, "USD", "USD",
            isEnabled: true, sortOrder: 0, categories: []);
        tld.UpdateSellPrices(
            register: new Dictionary<int, decimal> { [3] = 27m, [1] = 10m, [2] = 19.5m },
            transfer: new Dictionary<int, decimal> { [1] = 8m },
            renew: new Dictionary<int, decimal> { [1] = 11m });
        context.TldConfigs.Add(tld);
        await context.SaveChangesAsync(CancellationToken.None);
        cache.Remove("tld-pricing-configs");

        try
        {
            var body = await factory.CreateClient()
                .GetFromJsonAsync<JsonElement>("/api/domains/tld-pricing?currency=USD");

            var register = body.GetProperty("pricing").EnumerateArray().Single(entry => entry.GetProperty("tld").GetString() == tld.Tld).GetProperty("register")
                .EnumerateArray()
                .Select(price => (Years: price.GetProperty("years").GetInt32(), Amount: price.GetProperty("amount").GetDecimal()))
                .ToList();
            register.Should().Equal((1, 10m), (2, 19.5m), (3, 27m));
        }
        finally
        {
            context.TldConfigs.Remove(tld);
            await context.SaveChangesAsync(CancellationToken.None);
            cache.Remove("tld-pricing-configs");
        }
    }

    /// <summary>
    /// AMD prices follow the stored exchange rate: a 10 USD sell price is 10 / rate AMD, and
    /// changing the rate changes the price on the next call (no hard-coded table).
    /// </summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task GetTldPricing_AmdPricesFollowTheStoredExchangeRateAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
        var amd = await context.Currencies.SingleAsync(c => c.Code == "AMD");
        var originalRate = amd.RateToBase;
        var tld = TldConfig.Create(
            $"rate-{Guid.NewGuid():N}"[..12], RegistrarModule.NameAm, "USD", "USD",
            isEnabled: true, sortOrder: 0, categories: []);
        tld.UpdateSellPrices(
            register: new Dictionary<int, decimal> { [1] = 10m },
            transfer: new Dictionary<int, decimal> { [1] = 10m },
            renew: new Dictionary<int, decimal> { [1] = 10m });
        context.TldConfigs.Add(tld);

        try
        {
            var client = factory.CreateClient();
            foreach (var rate in new[] { 0.0025m, 0.005m })
            {
                amd.UpdateRate(rate);
                await context.SaveChangesAsync(CancellationToken.None);
                cache.Remove("tld-pricing-configs");

                var body = await client.GetFromJsonAsync<JsonElement>("/api/domains/tld-pricing?currency=AMD");

                var oneYear = body.GetProperty("pricing").EnumerateArray().Single(entry => entry.GetProperty("tld").GetString() == tld.Tld).GetProperty("register")
                    .EnumerateArray().Single(price => price.GetProperty("years").GetInt32() == 1);
                oneYear.GetProperty("amount").GetDecimal().Should().Be(amd.Round(10m / rate));
            }
        }
        finally
        {
            amd.UpdateRate(originalRate);
            context.TldConfigs.Remove(tld);
            await context.SaveChangesAsync(CancellationToken.None);
            cache.Remove("tld-pricing-configs");
        }
    }
}
