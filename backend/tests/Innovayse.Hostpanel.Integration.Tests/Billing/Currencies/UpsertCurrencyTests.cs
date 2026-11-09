using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Innovayse.Hostpanel.Integration.Tests.Billing.Currencies;

/// <summary>
/// <c>PUT /api/admin/currencies/{code}</c> against Postgres: creating a currency row and rewriting
/// it in place on a second call.
/// </summary>
/// <param name="factory">The shared API + PostgreSQL host.</param>
public sealed class UpsertCurrencyTests(IntegrationTestFactory factory) : IClassFixture<IntegrationTestFactory>
{
    /// <summary>A client carrying the seeded admin's bearer token.</summary>
    /// <returns>An authenticated client.</returns>
    private async Task<HttpClient> AdminClientAsync()
    {
        var client = factory.CreateClient();
        var token = await factory.GetAdminTokenAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>PUT on a new code creates the row, and PUT again rewrites it in place.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PutCreatesThenRewritesACurrency()
    {
        var client = await AdminClientAsync();

        var created = await client.PutAsJsonAsync("/api/admin/currencies/EUR",
            new { numeric = "978", prefix = "€", suffix = "", decimals = 2, rateToBase = 1.1m, isEnabled = true });
        created.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var rewritten = await client.PutAsJsonAsync("/api/admin/currencies/eur",
            new { numeric = "978", prefix = "€", suffix = "", decimals = 2, rateToBase = 1.2m, isEnabled = false });
        rewritten.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await client.GetFromJsonAsync<List<JsonElement>>("/api/admin/currencies");
        var eur = list!.Single(r => r.GetProperty("code").GetString() == "EUR");
        eur.GetProperty("rateToBase").GetDecimal().Should().Be(1.2m);
        eur.GetProperty("isEnabled").GetBoolean().Should().BeFalse();
        eur.GetProperty("isBase").GetBoolean().Should().BeFalse();
    }

    /// <summary>Upserting a currency is not for anonymous callers.</summary>
    /// <returns>A task that completes when the test has run.</returns>
    [Fact]
    public async Task PutRefusesAnonymous()
    {
        var response = await factory.CreateClient().PutAsJsonAsync("/api/admin/currencies/EUR",
            new { numeric = "978", prefix = "€", suffix = "", decimals = 2, rateToBase = 1.1m, isEnabled = true });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
