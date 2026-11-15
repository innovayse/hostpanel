using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Settings;
using Innovayse.Hostpanel.Domain.Settings.Enums;
using Innovayse.Hostpanel.Infrastructure.Currency;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Innovayse.Hostpanel.Integration.Tests.Billing.Currencies;

/// <summary>
/// Which bank "update rates" asks, chosen by the <c>billing.exchange_rates.provider</c> setting: the real
/// source selection and both real adapters, with the two banks' sites replaced by stub servers. The
/// default keeps the Central Bank of Armenia, the choice applies to the very next refresh, an invalid value
/// is refused or read as the default, and an outage at the chosen bank leaves the stored rates alone.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class ExchangeRateProviderSelectionTests(IntegrationTestFactory factory)
    : IClassFixture<IntegrationTestFactory>, IAsyncLifetime
{
    /// <summary>What the Armenian bulletin says one US dollar costs, in drams.</summary>
    private const decimal AmdPerUsd = 400m;

    /// <summary>What the Armenian bulletin says one euro costs, in drams.</summary>
    private const decimal AmdPerEur = 480m;

    /// <summary>What the European bank's set says one euro buys, in US dollars.</summary>
    private const decimal UsdPerEur = 1.25m;

    /// <summary>The loopback server standing in for the Central Bank of Armenia's site.</summary>
    private StubHttpServer cba = null!;

    /// <summary>The loopback server standing in for the European Central Bank's site.</summary>
    private StubHttpServer ecb = null!;

    /// <summary>Starts both stub bank servers before each test.</summary>
    /// <returns>A task that completes when both servers listen.</returns>
    public async Task InitializeAsync()
    {
        cba = await StubHttpServer.StartAsync();
        ecb = await StubHttpServer.StartAsync();
    }

    /// <summary>Stops both stub bank servers after each test.</summary>
    /// <returns>A task that completes when both servers are down.</returns>
    public async Task DisposeAsync()
    {
        await cba.DisposeAsync();
        await ecb.DisposeAsync();
    }

    /// <summary>A fresh installation is on the national bank, so nothing changes for it.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheDefaultProviderIsTheNationalBank_AndItsRatesAreWrittenAsTheyAlwaysWereAsync()
    {
        var client = await AdminClientAsync();
        cba.Enqueue($$"""{"USD":"{{AmdPerUsd}}"}""");

        (await StoredProviderAsync(client)).Should().Be(nameof(ExchangeRateProvider.Cba));
        (await client.PostAsync("/api/admin/currencies/update-rates", content: null)).StatusCode.Should().Be(HttpStatusCode.OK);

        cba.Requests.Should().ContainSingle();
        ecb.Requests.Should().BeEmpty();
        (await RateOfAsync(client, "AMD")).Should().Be(1m / AmdPerUsd);
    }

    /// <summary>With the European bank chosen its feed is read, the rates are re-expressed against the base and an unquoted currency is reported.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task TheEuropeanBankChosen_WritesItsRatesAgainstTheBaseAsync()
    {
        var client = await AdminClientAsync();
        await EnsureEuroAsync();
        ecb.Enqueue(HttpStatusCode.OK, EcbReplies.Daily, EcbReplies.ContentType);

        try
        {
            await SetProviderAsync(client, nameof(ExchangeRateProvider.Ecb));
            var response = await client.PostAsync("/api/admin/currencies/update-rates", content: null);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            body.RootElement.GetProperty("updated").EnumerateArray().Select(e => e.GetString()).Should().Contain("EUR");
            body.RootElement.GetProperty("missing").EnumerateArray().Select(e => e.GetString()).Should().Contain("AMD");
            (await RateOfAsync(client, "EUR")).Should().BeApproximately(UsdPerEur, 0.0000001m);
            ecb.Requests.Should().ContainSingle();
            cba.Requests.Should().BeEmpty();
        }
        finally
        {
            await SetProviderAsync(client, nameof(ExchangeRateProvider.Cba));
        }
    }

    /// <summary>A change of the setting applies to the next refresh, without a restart, and switches back the same way.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ChangingTheProvider_AppliesToTheNextRefreshAsync()
    {
        var client = await AdminClientAsync();
        await EnsureEuroAsync();
        ecb.Enqueue(HttpStatusCode.OK, EcbReplies.Daily, EcbReplies.ContentType);
        cba.Enqueue($$"""{"USD":"{{AmdPerUsd}}","EUR":"{{AmdPerEur}}"}""");

        try
        {
            await SetProviderAsync(client, nameof(ExchangeRateProvider.Ecb));
            await client.PostAsync("/api/admin/currencies/update-rates", content: null);
            ecb.Requests.Should().ContainSingle();
            cba.Requests.Should().BeEmpty();

            await SetProviderAsync(client, nameof(ExchangeRateProvider.Cba));
            await client.PostAsync("/api/admin/currencies/update-rates", content: null);
            ecb.Requests.Should().ContainSingle();
            cba.Requests.Should().ContainSingle();
            (await RateOfAsync(client, "EUR")).Should().Be(AmdPerEur / AmdPerUsd);
        }
        finally
        {
            await SetProviderAsync(client, nameof(ExchangeRateProvider.Cba));
        }
    }

    /// <summary>A name that is no provider is refused and the stored choice stays; a provider in any case is stored in its canonical spelling.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnUnknownProviderIsRefused_AndTheCaseOfAKnownOneIsNormalisedAsync()
    {
        var client = await AdminClientAsync();
        var settingId = await AdminTestSeeds.FindSettingIdAsync(client, BillingSettingKeys.RateProvider);

        try
        {
            var refused = await client.PutAsJsonAsync($"/api/admin/settings/{settingId}", new { value = "Boe" });
            refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            await AdminTestSeeds.AssertCodeAsync(refused, HostpanelErrorCodes.INVALID_SETTING_VALUE);
            (await StoredProviderAsync(client)).Should().Be(nameof(ExchangeRateProvider.Cba));

            (await client.PutAsJsonAsync($"/api/admin/settings/{settingId}", new { value = "ecb" })).StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await StoredProviderAsync(client)).Should().Be(nameof(ExchangeRateProvider.Ecb));
        }
        finally
        {
            await SetProviderAsync(client, nameof(ExchangeRateProvider.Cba));
        }
    }

    /// <summary>A row that holds something that is no provider, written behind the API's back, reads as the default and never breaks a refresh.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task ADamagedRow_ReadsAsTheNationalBankAsync()
    {
        var client = await AdminClientAsync();
        cba.Enqueue($$"""{"USD":"{{AmdPerUsd}}"}""");

        try
        {
            using (var scope = factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<IHostpanelDbContext>();
                await context.Settings.Where(s => s.Key == BillingSettingKeys.RateProvider)
                    .ExecuteUpdateAsync(set => set.SetProperty(s => s.Value, "Boe"));
            }

            (await client.PostAsync("/api/admin/currencies/update-rates", content: null)).StatusCode.Should().Be(HttpStatusCode.OK);

            cba.Requests.Should().ContainSingle();
            ecb.Requests.Should().BeEmpty();
        }
        finally
        {
            await SetProviderAsync(client, nameof(ExchangeRateProvider.Cba));
        }
    }

    /// <summary>An outage at the chosen bank is the fixed 503 with <c>Retry-After</c>, and no stored rate moves.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task AnOutageAtTheChosenBank_IsUnavailableAndLeavesTheRatesAsync()
    {
        var client = await AdminClientAsync();
        await EnsureEuroAsync();
        ecb.Enqueue(HttpStatusCode.ServiceUnavailable, "down", "text/plain");
        var before = await RateOfAsync(client, "EUR");

        try
        {
            await SetProviderAsync(client, nameof(ExchangeRateProvider.Ecb));
            var response = await client.PostAsync("/api/admin/currencies/update-rates", content: null);

            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            response.Headers.RetryAfter.Should().NotBeNull();
            await AdminTestSeeds.AssertCodeAsync(response, HostpanelErrorCodes.EXCHANGE_RATE_SOURCE_UNAVAILABLE);
            (await RateOfAsync(client, "EUR")).Should().Be(before);
        }
        finally
        {
            await SetProviderAsync(client, nameof(ExchangeRateProvider.Cba));
        }
    }

    /// <summary>Builds a host whose two bank adapters talk to the stub servers, and an admin client on it.</summary>
    /// <returns>An authenticated client.</returns>
    private async Task<HttpClient> AdminClientAsync()
    {
        var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.AddHttpClient<CbaExchangeRateSource>().ConfigureHttpClient(c => c.BaseAddress = cba.BaseAddress);
                services.AddHttpClient<EcbExchangeRateSource>().ConfigureHttpClient(c => c.BaseAddress = ecb.BaseAddress);
            }));

        var client = host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.GetAdminTokenAsync());
        return client;
    }

    /// <summary>Makes sure the euro is a stored, non-base currency, so the European bank has one to quote.</summary>
    /// <returns>A task that completes when it is stored.</returns>
    private Task EnsureEuroAsync() => BillingTestSeeds.EnsureCurrencyAsync(factory, "EUR", "978", 1m);

    /// <summary>Writes the provider setting through the admin API.</summary>
    /// <param name="client">An authenticated admin client.</param>
    /// <param name="provider">The provider name to store.</param>
    /// <returns>A task that completes when it is stored.</returns>
    private static async Task SetProviderAsync(HttpClient client, string provider)
    {
        var settingId = await AdminTestSeeds.FindSettingIdAsync(client, BillingSettingKeys.RateProvider);
        (await client.PutAsJsonAsync($"/api/admin/settings/{settingId}", new { value = provider }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>Reads the stored provider setting through the admin API.</summary>
    /// <param name="client">An authenticated admin client.</param>
    /// <returns>The stored value.</returns>
    private static async Task<string?> StoredProviderAsync(HttpClient client)
    {
        var rows = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/settings");
        return rows!.Single(r => r.GetProperty("key").GetString() == BillingSettingKeys.RateProvider).GetProperty("value").GetString();
    }

    /// <summary>Reads the stored rate of a currency through the admin API.</summary>
    /// <param name="client">An authenticated admin client.</param>
    /// <param name="code">The currency code.</param>
    /// <returns>The rate to the base currency.</returns>
    private static async Task<decimal> RateOfAsync(HttpClient client, string code)
    {
        var rows = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/currencies");
        return rows!.Single(r => r.GetProperty("code").GetString() == code).GetProperty("rateToBase").GetDecimal();
    }
}
